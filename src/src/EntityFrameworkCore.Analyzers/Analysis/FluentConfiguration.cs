using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore.Analyzers.Analysis;

/// <summary>
/// The subset of <c>OnModelCreating</c> fluent configuration the generator interprets.
/// </summary>
/// <remarks>
/// Matching is syntactic: the analyser walks each invocation chain back to its owning
/// <c>Entity&lt;T&gt;()</c> call. Configuration applied indirectly — through a loop, a helper method, or
/// <c>ApplyConfigurationsFromAssembly</c> — is not visible and is reported as ASPIREC4010.
/// </remarks>
sealed class FluentConfiguration
{
	/// <summary>A per-entity bag of interpreted configuration, keyed by entity display name.</summary>
	public Dictionary<string, EntityConfiguration> Entities { get; } = [with(StringComparer.Ordinal)];

	/// <summary>The schema set via <c>HasDefaultSchema</c>, when found.</summary>
	public string? DefaultSchema { get; private set; }

	/// <summary>Configuration sites the analyser could not interpret.</summary>
	public List<(string Description, Location? Location)> Uninterpretable { get; } = [];

	/// <summary>Reads the supported fluent configuration out of an <c>OnModelCreating</c> body.</summary>
	public static FluentConfiguration Read(MethodDeclarationSyntax method, SemanticModel semanticModel)
	{
		FluentConfiguration config = new();

		foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			var name = GetInvokedName(invocation);

			if (name is null)
			{
				continue;
			}

			switch (name)
			{
				case EfTypes.Fluent.ApplyConfiguration:
				case EfTypes.Fluent.ApplyConfigurationsFromAssembly:
					config.Uninterpretable.Add((name, invocation.GetLocation()));
					continue;

				case EfTypes.Fluent.HasDefaultSchema:
					config.DefaultSchema = GetStringArgument(invocation, 0) ?? config.DefaultSchema;
					continue;

				default:
					break;
			}

			var entityType = ResolveConfiguredEntity(invocation, semanticModel);

			if (entityType is null)
			{
				continue;
			}

			var entity = config.GetOrAdd(entityType.ToDisplayString());

			switch (name)
			{
				case EfTypes.Fluent.ToTable:
					entity.TableName = GetStringArgument(invocation, 0) ?? entity.TableName;
					entity.Schema = GetStringArgument(invocation, 1) ?? entity.Schema;
					break;

				case EfTypes.Fluent.HasKey:
					foreach (var member in GetLambdaMembers(invocation))
					{
						entity.KeyProperties.Add(member);
					}

					break;

				case EfTypes.Fluent.Ignore:
					foreach (var member in GetLambdaMembers(invocation))
					{
						entity.IgnoredProperties.Add(member);
					}

					break;

				case EfTypes.Fluent.HasIndex:
					// IsUnique() is applied to the chain that owns this HasIndex call.
					if (IsFollowedBy(invocation, EfTypes.Fluent.IsUnique))
					{
						foreach (var member in GetLambdaMembers(invocation))
						{
							entity.UniqueProperties.Add(member);
						}
					}

					break;

				case EfTypes.Fluent.HasColumnName:
				case EfTypes.Fluent.HasColumnType:
				case EfTypes.Fluent.HasMaxLength:
				case EfTypes.Fluent.IsRequired:
				case EfTypes.Fluent.HasComment:
					ApplyPropertyConfiguration(entity, invocation, name);
					break;

				default:
					break;
			}
		}

		return config;
	}

	EntityConfiguration GetOrAdd(string key)
	{
		if (!Entities.TryGetValue(key, out var entity))
		{
			entity = new EntityConfiguration();
			Entities[key] = entity;
		}

		return entity;
	}

	static void ApplyPropertyConfiguration(
		EntityConfiguration entity,
		InvocationExpressionSyntax invocation,
		string name
	)
	{
		var propertyName = ResolveConfiguredProperty(invocation);

		if (propertyName is null)
		{
			return;
		}

		var property = entity.GetOrAddProperty(propertyName);

		switch (name)
		{
			case EfTypes.Fluent.HasColumnName:
				property.ColumnName = GetStringArgument(invocation, 0) ?? property.ColumnName;
				break;

			case EfTypes.Fluent.HasColumnType:
				property.ColumnType = GetStringArgument(invocation, 0) ?? property.ColumnType;
				break;

			case EfTypes.Fluent.HasMaxLength:
				property.MaxLength = GetIntArgument(invocation, 0) ?? property.MaxLength;
				break;

			case EfTypes.Fluent.IsRequired:
				// IsRequired() defaults to true; IsRequired(false) makes the column optional.
				property.IsRequired = GetBoolArgument(invocation, 0) ?? true;
				break;

			case EfTypes.Fluent.HasComment:
				property.Comment = GetStringArgument(invocation, 0) ?? property.Comment;
				break;

			default:
				break;
		}
	}

	/// <summary>Walks an invocation chain back to the nearest <c>Property(e =&gt; e.X)</c> call.</summary>
	static string? ResolveConfiguredProperty(InvocationExpressionSyntax invocation)
	{
		foreach (var ancestor in WalkReceivers(invocation))
		{
			if (GetInvokedName(ancestor) == EfTypes.Fluent.Property)
			{
				var members = GetLambdaMembers(ancestor);

				return members.Count > 0 ? members[0] : GetStringArgument(ancestor, 0);
			}
		}

		return null;
	}

	/// <summary>Walks an invocation chain back to the owning <c>Entity&lt;T&gt;()</c> call.</summary>
	static INamedTypeSymbol? ResolveConfiguredEntity(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
	{
		foreach (var ancestor in WalkReceivers(invocation))
		{
			if (GetInvokedName(ancestor) != EfTypes.Fluent.Entity)
			{
				continue;
			}

			if (
				ancestor.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic }
				&& generic.TypeArgumentList.Arguments.Count == 1
			)
			{
				return semanticModel.GetSymbolInfo(generic.TypeArgumentList.Arguments[0]).Symbol as INamedTypeSymbol;
			}
		}

		return null;
	}

	/// <summary>Yields the invocation itself and every invocation to its left in the chain.</summary>
	static IEnumerable<InvocationExpressionSyntax> WalkReceivers(InvocationExpressionSyntax invocation)
	{
		var current = (ExpressionSyntax?)invocation;

		while (current is not null)
		{
			if (current is InvocationExpressionSyntax inner)
			{
				yield return inner;
				current = (inner.Expression as MemberAccessExpressionSyntax)?.Expression;

				continue;
			}

			current = (current as MemberAccessExpressionSyntax)?.Expression;
		}
	}

	/// <summary>Determines whether the chain containing this invocation later calls <paramref name="name"/>.</summary>
	static bool IsFollowedBy(InvocationExpressionSyntax invocation, string name)
	{
		var current = invocation.Parent;

		while (current is MemberAccessExpressionSyntax or InvocationExpressionSyntax)
		{
			if (current is InvocationExpressionSyntax outer && GetInvokedName(outer) == name)
			{
				return true;
			}

			current = current.Parent;
		}

		return false;
	}

	static string? GetInvokedName(InvocationExpressionSyntax invocation) =>
		invocation.Expression switch
		{
			MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
			_ => null,
		};

	/// <summary>Extracts <c>e =&gt; e.A</c> and <c>e =&gt; new { e.A, e.B }</c> member names.</summary>
	static List<string> GetLambdaMembers(InvocationExpressionSyntax invocation)
	{
		List<string> members = [];

		if (invocation.ArgumentList.Arguments.Count == 0)
		{
			return members;
		}

		var body = (invocation.ArgumentList.Arguments[0].Expression as LambdaExpressionSyntax)?.Body;

		switch (body)
		{
			case MemberAccessExpressionSyntax single:
				members.Add(single.Name.Identifier.ValueText);
				break;

			case AnonymousObjectCreationExpressionSyntax anonymous:
				foreach (var initialiser in anonymous.Initializers)
				{
					if (initialiser.Expression is MemberAccessExpressionSyntax member)
					{
						members.Add(member.Name.Identifier.ValueText);
					}
				}

				break;

			default:
				break;
		}

		return members;
	}

	static string? GetStringArgument(InvocationExpressionSyntax invocation, int index) =>
		GetLiteral(invocation, index) is LiteralExpressionSyntax literal && literal.Token.Value is string value
			? value
			: null;

	static int? GetIntArgument(InvocationExpressionSyntax invocation, int index) =>
		GetLiteral(invocation, index) is LiteralExpressionSyntax literal && literal.Token.Value is int value
			? value
			: null;

	static bool? GetBoolArgument(InvocationExpressionSyntax invocation, int index) =>
		GetLiteral(invocation, index) is LiteralExpressionSyntax literal && literal.Token.Value is bool value
			? value
			: null;

	static ExpressionSyntax? GetLiteral(InvocationExpressionSyntax invocation, int index) =>
		invocation.ArgumentList.Arguments.Count > index ? invocation.ArgumentList.Arguments[index].Expression : null;
}

/// <summary>Interpreted fluent configuration for a single entity type.</summary>
sealed class EntityConfiguration
{
	public string? TableName { get; set; }

	public string? Schema { get; set; }

	public HashSet<string> KeyProperties { get; } = [with(StringComparer.Ordinal)];

	public HashSet<string> IgnoredProperties { get; } = [with(StringComparer.Ordinal)];

	public HashSet<string> UniqueProperties { get; } = [with(StringComparer.Ordinal)];

	public Dictionary<string, PropertyConfiguration> Properties { get; } = [with(StringComparer.Ordinal)];

	/// <summary>Gets, creating if needed, the configuration bag for a property.</summary>
	public PropertyConfiguration GetOrAddProperty(string name)
	{
		if (!Properties.TryGetValue(name, out var property))
		{
			property = new PropertyConfiguration();
			Properties[name] = property;
		}

		return property;
	}
}

/// <summary>Interpreted fluent configuration for a single property.</summary>
sealed class PropertyConfiguration
{
	public string? ColumnName { get; set; }

	public string? ColumnType { get; set; }

	public int? MaxLength { get; set; }

	public bool? IsRequired { get; set; }

	public string? Comment { get; set; }
}
