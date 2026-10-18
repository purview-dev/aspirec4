using Aspire.Hosting.AspireC4.Lifecycle;
using Aspire.Hosting.AspireC4.LikeC4.Annotations;
using Aspire.Hosting.AspireC4.LikeC4.Icons;
using Aspire.Hosting.AspireC4.LikeC4.Models;

namespace Aspire.Hosting.AspireC4.LikeC4;

/// <summary>
/// Builds a <see cref="LikeC4Model"/> from the Aspire application model resources.
/// </summary>
static class ModelBuilder
{
	// Aspire's well-known relationship type for wait-for dependencies — these are infrastructure
	// concerns and should not appear as architecture relationships in the diagram.
	const string WaitForRelationshipType = "WaitFor";

	/// <summary>Builds a <see cref="LikeC4Model"/> from a list of Aspire resources.</summary>
	/// <param name="resources">All resources in the Aspire app model.</param>
	/// <param name="resourceStates">
	/// Optional mapping of resource name → current <see cref="KnownResourceStates"/> value.
	/// When provided, the corresponding diagram element is coloured to reflect the live state.
	/// </param>
	/// <param name="autoIconsEnabled">When <see langword="true"/>, infers icons from resource type/name.</param>
	/// <param name="aspireMetadataInclusion">Controls which Aspire runtime metadata is injected into elements.</param>
	/// <param name="normaliseMetadataBehaviour">Controls how invalid characters in metadata keys are handled.</param>
	/// <param name="iconResolvers">
	/// Optional list of custom icon resolvers evaluated before built-in auto-icon inference.
	/// Each resolver receives a <see cref="IconResolverContext"/> and should return a non-null icon string
	/// to override the default, or <see langword="null"/> to defer to the next resolver or built-in inference.
	/// </param>
	/// <param name="includeDashboardLinks">
	/// When <see langword="true"/> and <paramref name="dashboardBaseUrl"/> is provided, adds links from each element
	/// to the Aspire dashboard console logs and structured logs pages for that resource.
	/// </param>
	/// <param name="dashboardBaseUrl">
	/// Base URL of the Aspire dashboard (e.g. <c>https://localhost:15086</c>). When provided and
	/// <paramref name="includeDashboardLinks"/> is <see langword="true"/>, dashboard deep-links are injected
	/// into each element's <see cref="LikeC4Element.Links"/> collection.
	/// </param>
	/// <param name="dashboardBrowserToken">
	/// The Aspire dashboard browser token (from <c>AppHost:BrowserToken</c> configuration). When provided,
	/// dashboard links use a <c>/login?t=…&amp;returnUrl=…</c> redirect URL so clicking the link in LikeC4
	/// authenticates the browser before navigating to the resource page.
	/// </param>
	/// <param name="stateTagMap">Optional mapping of resource state to tag strings.</param>
	/// <param name="resourceSnapshotUrls">
	/// Optional mapping of resource name to externally-accessible endpoint URLs captured from Aspire resource
	/// snapshots. When present for a resource, these URLs are used for endpoint links in preference to reading
	/// <see cref="EndpointAnnotation.AllocatedEndpoint"/> directly, ensuring
	/// the correct public port is used (the same source the Aspire dashboard uses).
	/// </param>
	/// <param name="excludedResourceTypes">
	/// Optional set of resource types to exclude from the model. A resource is excluded when its runtime type is
	/// the same as, or a subclass of, any type in this set. When <see langword="null"/>, no type-based exclusion
	/// is applied. See <see cref="AspireC4DiagramOptions.ExcludedResourceTypes"/> for the configured default.
	/// </param>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Design",
		"CA1054:URI-like parameters should not be strings",
		Justification = "Dashboard base URL is composed via string concatenation consistent with LikeC4Link.Uri; wrapping as System.Uri would add unnecessary conversion overhead"
	)]
	public static LikeC4Model Build(
		IReadOnlyList<IResource> resources,
		IReadOnlyDictionary<string, string?>? resourceStates = null,
		bool autoIconsEnabled = true,
		AspireMetadataInclusion aspireMetadataInclusion = AspireMetadataInclusion.All,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise,
		IReadOnlyList<Func<IconResolverContext, string?>>? iconResolvers = null,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyDictionary<string, string?>? stateTagMap = null,
		IReadOnlyDictionary<string, IReadOnlyList<(string Url, string Name)>>? resourceSnapshotUrls = null,
		IReadOnlySet<Type>? excludedResourceTypes = null
	)
	{
		ArgumentNullException.ThrowIfNull(resources);

		var visibleResources = BuildVisibleSet(resources, excludedResourceTypes);

		// Build a name → resource lookup so we can resolve "surrogate" resources.
		// When an Azure resource (e.g. AzureRedisCacheResource) is replaced by a local
		// container via RunAsContainer(), the original Azure resource is hidden and a
		// ContainerResource with the same name becomes the visible counterpart.
		// WithReference() still annotates with the hidden Azure resource, so we need
		// this lookup to resolve the visible surrogate by name.
		var visibleByName = visibleResources
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		// Hidden resources (e.g. AzurePostgresFlexibleServerResource) carry richer type info
		// than the visible surrogate (e.g. a generic ContainerResource). We pass the hidden
		// original to the element builder so the icon matcher can use its type name to select
		// the correct azure icon (e.g. azure:azure-database-postgre-sql-server).
		var hiddenByName = resources
			.Where(r => !visibleResources.Contains(r))
			.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

		// Build a name → DSL-identifier map. A LikeC4DslIdAnnotation lets any resource emit
		// under a different identifier than its Aspire resource name (used, for example, to
		// normalise the AspireC4 sidecar to "aspirec4" regardless of whether the Docker
		// container or the local-CLI executable is active).
		// A LikeC4ExistingElementAnnotation takes precedence: the resource is emitted against an element
		// a hand-authored project already declares, and every reference to it — relationships, nesting,
		// view groups — has to use that identifier rather than the Aspire resource name.
		var dslIdByName = visibleResources.ToDictionary(
			r => r.Name,
			r =>
				r.Annotations.OfType<LikeC4ExistingElementAnnotation>().LastOrDefault()?.ElementId
				?? r.Annotations.OfType<LikeC4DSLIdAnnotation>().LastOrDefault()?.DSLId
				?? r.Name,
			StringComparer.OrdinalIgnoreCase
		);

#pragma warning disable IDE0028 // Simplify collection initialization
		List<LikeC4Element> elements = new(visibleResources.Count);
#pragma warning restore IDE0028 // Simplify collection initialization
		List<LikeC4Relationship> relationships = [];
		HashSet<(string Source, string Target)> visitedRelationships = [];

		foreach (var resource in visibleResources)
		{
			var state = resourceStates is not null && resourceStates.TryGetValue(resource.Name, out var s) ? s : null;

			elements.Add(
				BuildElement(
					resource,
					dslIdByName[resource.Name],
					state,
					autoIconsEnabled,
					aspireMetadataInclusion,
					normaliseMetadataBehaviour,
					hiddenByName.GetValueOrDefault(resource.Name),
					iconResolvers,
					includeDashboardLinks,
					dashboardBaseUrl,
					dashboardBrowserToken,
					stateTagMap,
					resourceSnapshotUrls?.GetValueOrDefault(resource.Name)
				)
			);
			CollectRelationships(
				resource,
				visibleResources,
				visibleByName,
				dslIdByName,
				relationships,
				visitedRelationships,
				normaliseMetadataBehaviour
			);
		}

		return new LikeC4Model { Elements = elements, Relationships = relationships };
	}

	/// <summary>
	/// Returns the names of all resources that would be included in the diagram
	/// (i.e. not excluded or hidden). Useful for filtering state-change notifications
	/// to only relevant resources.
	/// </summary>
	public static IReadOnlySet<string> GetVisibleResourceNames(
		IReadOnlyList<IResource> resources,
		IReadOnlySet<Type>? excludedResourceTypes = null
	)
	{
		ArgumentNullException.ThrowIfNull(resources);

		return BuildVisibleSet(resources, excludedResourceTypes)
			.Select(r => r.Name)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	static HashSet<IResource> BuildVisibleSet(
		IReadOnlyList<IResource> resources,
		IReadOnlySet<Type>? excludedResourceTypes
	)
	{
#pragma warning disable IDE0028 // Simplify collection initialization
		HashSet<IResource> visible = new(resources.Count);
#pragma warning restore IDE0028 // Simplify collection initialization

		foreach (var resource in resources)
		{
			if (ShouldExclude(resource, excludedResourceTypes))
			{
				continue;
			}

			visible.Add(resource);
		}

		return visible;
	}

	static bool ShouldExclude(IResource resource, IReadOnlySet<Type>? excludedResourceTypes)
	{
		// Explicitly excluded by the user via annotation.
		if (resource.Annotations.OfType<ExcludeFromLikeC4Annotation>().Any())
		{
			return true;
		}

		// Hidden resources (e.g. internal Aspire infrastructure) should not appear.
		var snapshot = resource.Annotations.OfType<ResourceSnapshotAnnotation>().FirstOrDefault();
		if (snapshot?.InitialSnapshot.IsHidden == true)
		{
			return true;
		}

		// Type-based exclusion: excluded when the resource is an instance of any excluded type
		// (exact match or subclass). Defaults include ParameterResource to suppress secrets.
		if (excludedResourceTypes is { Count: > 0 })
		{
			var resourceType = resource.GetType();
			foreach (var excludedType in excludedResourceTypes)
			{
				if (resourceType.IsAssignableTo(excludedType))
				{
					return true;
				}
			}
		}

		return resource.Name == AspireC4LifecycleHook.AspireDashboardResourceName;
	}

	static LikeC4Element BuildElement(
		IResource resource,
		string dslId,
		string? state,
		bool autoIconsEnabled,
		AspireMetadataInclusion aspireMetadataInclusion = AspireMetadataInclusion.All,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour = NormaliseMetadataBehaviour.Normalise,
		IResource? hiddenOriginal = null,
		IReadOnlyList<Func<IconResolverContext, string?>>? iconResolvers = null,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyDictionary<string, string?>? stateTagMap = null,
		IReadOnlyList<(string Url, string Name)>? snapshotEndpointUrls = null
	)
	{
		var details = resource.Annotations.OfType<LikeC4NodeDetailsAnnotation>().LastOrDefault();
		var inferredTechnology = InferTechnology(resource);
		var extendsExisting = resource.Annotations.OfType<LikeC4ExistingElementAnnotation>().Any();

		var label = details?.Label ?? resource.Name;
		var (technology, icon) = ResolveAppearance(
			resource,
			details,
			inferredTechnology,
			extendsExisting,
			autoIconsEnabled,
			hiddenOriginal,
			iconResolvers
		);
		var kind = details?.Kind ?? InferKind(resource);
		var parentName = resource is IResourceWithParent { Parent: { } parent }
			? (parent.Annotations.OfType<LikeC4DSLIdAnnotation>().LastOrDefault()?.DSLId ?? parent.Name)
			: null;
		var group = resource.Annotations.OfType<LikeC4GroupAnnotation>().LastOrDefault()?.GroupName;

		var userMetadata = NormaliseMetadataKeys(details?.Metadata ?? [], normaliseMetadataBehaviour);
		var userLinks = details?.Links ?? [];
		var (autoMetadata, autoLinks) = BuildAspireData(
			resource,
			aspireMetadataInclusion,
			userMetadata,
			userLinks,
			includeDashboardLinks,
			dashboardBaseUrl,
			dashboardBrowserToken,
			snapshotEndpointUrls
		);

		// Prepend the state tag (when configured) to the element's user-defined tags.
		IReadOnlyList<string> stateTags = [];
		if (state is not null)
		{
			var stateTag =
				stateTagMap is not null && stateTagMap.TryGetValue(state, out var tagOverride)
					? tagOverride
					: $"aspire-run-state-{state.ToLowerInvariantSafe()}";
			if (stateTag is not null)
				stateTags = [Helpers.NormaliseTag(stateTag)];
		}

		// Prose is dropped for the same reason as technology and icon: LikeC4's extend body has no
		// description or summary, so the project the element comes from keeps both.
		var description = extendsExisting ? null : details?.Description;
		var summary = extendsExisting ? null : details?.Summary;

		return new()
		{
			Name = dslId,
			Label = label,
			Kind = kind,
			Technology = technology,
			Description = description,
			Summary = summary,
			Icon = icon,
			ParentName = parentName,
			State = state,
			Tags = [.. stateTags, .. details?.Tags ?? []],
			Links = [.. userLinks, .. autoLinks],
			Metadata = [.. userMetadata, .. autoMetadata],
			Group = group,
			ExtendsExistingElement = extendsExisting,
		};
	}

	/// <summary>Resolves the technology label and icon shown on an element.</summary>
	/// <remarks>
	/// Both are dropped when extending an element a hand-authored project already describes: LikeC4's
	/// <c>extend</c> body accepts tags, links, metadata and nested declarations only — <c>technology</c>
	/// and <c>icon</c> are parse errors there — so the project remains their sole source.
	/// </remarks>
	static (string? Technology, string? Icon) ResolveAppearance(
		IResource resource,
		LikeC4NodeDetailsAnnotation? details,
		string? inferredTechnology,
		bool extendsExisting,
		bool autoIconsEnabled,
		IResource? hiddenOriginal,
		IReadOnlyList<Func<IconResolverContext, string?>>? iconResolvers
	)
	{
		if (extendsExisting)
		{
			return (null, null);
		}

		return (
			details?.Technology ?? inferredTechnology,
			ResolveIcon(resource, details, inferredTechnology, autoIconsEnabled, hiddenOriginal, iconResolvers)
		);
	}

	/// <summary>
	/// Collects Aspire-derived metadata entries and links that are not already present
	/// in the user-provided sets. User-provided values always take precedence.
	/// </summary>
	static (IReadOnlyList<LikeC4Metadata> Metadata, IReadOnlyList<LikeC4Link> Links) BuildAspireData(
		IResource resource,
		AspireMetadataInclusion inclusion,
		IReadOnlyList<LikeC4Metadata> existingMetadata,
		IReadOnlyList<LikeC4Link> existingLinks,
		bool includeDashboardLinks = true,
		string? dashboardBaseUrl = null,
		string? dashboardBrowserToken = null,
		IReadOnlyList<(string Url, string Name)>? snapshotEndpointUrls = null
	)
	{
		List<LikeC4Metadata> metadata = [];
		List<LikeC4Link> links = [];

		if (inclusion.HasFlag(AspireMetadataInclusion.Metadata))
		{
			var existingKeys = existingMetadata.Select(m => m.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (!existingKeys.Contains("aspire-name"))
			{
				metadata.Add(new LikeC4Metadata("aspire-name", resource.Name));
			}

			var resourceType = resource
				.Annotations.OfType<ResourceSnapshotAnnotation>()
				.LastOrDefault()
				?.InitialSnapshot.ResourceType;

			if (!string.IsNullOrEmpty(resourceType) && !existingKeys.Contains("aspire-type"))
			{
				metadata.Add(new LikeC4Metadata("aspire-type", resourceType));
			}
		}

		if (inclusion.HasFlag(AspireMetadataInclusion.Links))
		{
			var existingUris = existingLinks.Select(l => l.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);

			// Prefer snapshot URLs (same source as Aspire dashboard, correct public port).
			// Fall back to EndpointAnnotation when no snapshot data is available yet
			// (e.g. initial generation before resources start, or in unit tests).
			if (snapshotEndpointUrls is not null)
			{
				foreach (var (url, name) in snapshotEndpointUrls)
				{
					if (existingUris.Add(url))
					{
						links.Add(new LikeC4Link(url, $"Endpoint: {name}"));
					}
				}
			}
			else
			{
				foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
				{
					if (endpoint.UriScheme is not "http" and not "https")
					{
						continue;
					}

					if (endpoint.AllocatedEndpoint is not { } ep || ep.Port <= 0)
					{
						continue;
					}

					var host = ep.Address is "0.0.0.0" ? "localhost" : ep.Address;
					var uri = $"{endpoint.UriScheme}://{host}:{ep.Port}";

					if (existingUris.Add(uri))
					{
						links.Add(new LikeC4Link(uri, $"Endpoint: {endpoint.Name}"));
					}
				}
			}

			if (includeDashboardLinks && dashboardBaseUrl is not null)
			{
				var consolePath = $"/consolelogs/resource/{Uri.EscapeDataString(resource.Name)}";
				var structuredPath = $"/structuredlogs/resource/{Uri.EscapeDataString(resource.Name)}";

				var consoleUrl = BuildDashboardLink(dashboardBaseUrl, consolePath, dashboardBrowserToken);
				var structuredUrl = BuildDashboardLink(dashboardBaseUrl, structuredPath, dashboardBrowserToken);

				if (existingUris.Add(consoleUrl))
				{
					links.Add(new LikeC4Link(consoleUrl, "Dashboard: Console Logs"));
				}

				if (existingUris.Add(structuredUrl))
				{
					links.Add(new LikeC4Link(structuredUrl, "Dashboard: Structured Logs"));
				}
			}
		}

		return (metadata, links);
	}

	static string BuildDashboardLink(string baseUrl, string path, string? browserToken)
	{
		if (browserToken is null)
			return $"{baseUrl}{path}";

		var encodedToken = Uri.EscapeDataString(browserToken);
		return $"{baseUrl}/login?t={encodedToken}&returnUrl={Uri.EscapeDataString(path)}";
	}

	/// <summary>
	/// Normalises a single metadata key according to the specified behaviour.
	/// Valid key characters are letters, digits, hyphens (<c>-</c>), and underscores (<c>_</c>).
	/// </summary>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="behaviour"/> is <see cref="NormaliseMetadataBehaviour.Throw"/>
	/// and <paramref name="key"/> contains invalid characters.
	/// </exception>
	static string NormaliseMetadataKey(string key, NormaliseMetadataBehaviour behaviour)
	{
		ArgumentNullException.ThrowIfNull(key);

		if (behaviour == NormaliseMetadataBehaviour.Throw)
		{
			return !IsValidMetadataKey(key)
				? throw new ArgumentException(
					$"Metadata key '{key}' contains invalid characters. "
						+ "Valid characters are letters, digits, hyphens (-), and underscores (_).",
					nameof(key)
				)
				: key;
		}

		var normalised = string.Create(
			key.Length,
			key,
			static (span, src) =>
			{
				for (var i = 0; i < src.Length; i++)
				{
					span[i] = IsValidMetadataKeyChar(src[i]) ? src[i] : '_';
				}
			}
		);

		return behaviour == NormaliseMetadataBehaviour.NormaliseLowercase
			? normalised.ToLowerInvariantSafe()
			: normalised;
	}

	/// <summary>
	/// Returns a new dictionary with all keys normalised according to <paramref name="behaviour"/>.
	/// When two keys normalise to the same value, the first occurrence is kept.
	/// </summary>
	/// <summary>
	/// Returns a new list with all keys normalised according to <paramref name="behaviour"/>.
	/// When two keys normalise to the same value, the first occurrence is kept.
	/// </summary>
	static IReadOnlyList<LikeC4Metadata> NormaliseMetadataKeys(
		IReadOnlyList<LikeC4Metadata> metadata,
		NormaliseMetadataBehaviour behaviour
	)
	{
		if (metadata.Count == 0)
		{
			return metadata;
		}

		List<LikeC4Metadata> results = [];
		foreach (var (key, value) in metadata)
		{
			var normalised = NormaliseMetadataKey(key, behaviour);
			results.Add(new LikeC4Metadata(normalised, value));
		}

		return results;
	}

	static bool IsValidMetadataKey(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}

		foreach (var c in key)
		{
			if (!IsValidMetadataKeyChar(c))
			{
				return false;
			}
		}

		return true;
	}

	static bool IsValidMetadataKeyChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-';

	static string InferKind(IResource resource) =>
		resource switch
		{
			ProjectResource => LikeC4ElementKind.Component,
			ContainerResource => LikeC4ElementKind.Container,
			// IResourceWithConnectionString before ExecutableResource to catch DB-like resources
			IResourceWithConnectionString => LikeC4ElementKind.Database,
			ExecutableResource => LikeC4ElementKind.Executable,
			_ => LikeC4ElementKind.System,
		};

	static string? InferTechnology(IResource resource) =>
		resource switch
		{
			ProjectResource => ".NET",
			ContainerResource container => container
				.Annotations.OfType<ContainerImageAnnotation>()
				.LastOrDefault()
				?.Image,
			_ => null,
		};

	static string? ResolveIcon(
		IResource resource,
		LikeC4NodeDetailsAnnotation? details,
		string? inferredTechnology,
		bool autoIconsEnabled,
		IResource? hiddenOriginal = null,
		IReadOnlyList<Func<IconResolverContext, string?>>? iconResolvers = null
	)
	{
		if (!string.IsNullOrWhiteSpace(details?.Icon))
		{
			return details.Icon;
		}

		// Custom resolvers run before auto-inference; first non-null result wins.
		if (iconResolvers is { Count: > 0 })
		{
			IconResolverContext ctx = new() { Resource = resource, HiddenOriginal = hiddenOriginal };
			foreach (var resolver in iconResolvers)
			{
				var resolved = resolver(ctx);
				if (resolved is not null)
				{
					return resolved;
				}
			}
		}

		return !(details?.AutoIconEnabled ?? autoIconsEnabled)
			? null
			: IconMatcher.TryInferIcon([
				details?.Technology,
				inferredTechnology,
				// The hidden original (e.g. AzurePostgresFlexibleServerResource) carries richer
				// type information than the visible surrogate (e.g. a generic ContainerResource).
				// Short name first — it has fewer noise tokens than the fully-qualified name.
				hiddenOriginal?.GetType().Name,
				hiddenOriginal?.GetType().FullName,
				resource.Annotations.OfType<ResourceSnapshotAnnotation>().LastOrDefault()?.InitialSnapshot.ResourceType,
				resource.GetType().FullName,
				resource.GetType().Name,
				resource.Name,
			]);
	}

	static void CollectRelationships(
		IResource resource,
		HashSet<IResource> visibleResources,
		Dictionary<string, IResource> visibleByName,
		Dictionary<string, string> dslIdByName,
		List<LikeC4Relationship> relationships,
		HashSet<(string, string)> visited,
		NormaliseMetadataBehaviour normaliseMetadataBehaviour
	)
	{
		var sourceDslId = dslIdByName.GetValueOrDefault(resource.Name, resource.Name);
		foreach (var annotation in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
		{
			// Skip infrastructure-only wait-for dependencies.
			if (annotation.Type == WaitForRelationshipType)
			{
				continue;
			}

			// Resolve the effective target: use the annotated resource directly if it is
			// visible, otherwise look for a visible surrogate with the same name.
			// The surrogate pattern arises with Azure resources run via RunAsContainer():
			// the original Azure resource is hidden and replaced by a ContainerResource
			// with the same name, but WithReference() still annotates with the Azure resource.
			var effectiveTarget = visibleResources.Contains(annotation.Resource)
				? annotation.Resource
				: visibleByName.GetValueOrDefault(annotation.Resource.Name);

			if (effectiveTarget is null)
			{
				continue;
			}

			var key = (sourceDslId, dslIdByName.GetValueOrDefault(effectiveTarget.Name, effectiveTarget.Name));
			if (!visited.Add(key))
			{
				continue;
			}

			// Look for a LikeC4-specific override. Keyed by target name; also checks the resolved
			// effective target for the Azure RunAsContainer() surrogate pattern.
			var details = resource
				.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>()
				.LastOrDefault(a =>
					string.Equals(a.TargetName, annotation.Resource.Name, StringComparison.OrdinalIgnoreCase)
					|| string.Equals(a.TargetName, effectiveTarget.Name, StringComparison.OrdinalIgnoreCase)
				);

			// Fall back to the Aspire relationship type as label when not a generic 'Reference'
			// or infrastructure 'WaitFor' annotation and no explicit label was provided.
			var inferredLabel = annotation.Type is not ("Reference" or WaitForRelationshipType)
				? annotation.Type
				: null;

			var relationshipMetadata = NormaliseMetadataKeys(details?.Metadata ?? [], normaliseMetadataBehaviour);
			var relationshipTags = details?.Tags ?? [];
			var relationshipKind = details?.Kind;

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = sourceDslId,
					TargetName = dslIdByName.GetValueOrDefault(effectiveTarget.Name, effectiveTarget.Name),
					Label = details?.Label ?? inferredLabel,
					Technology = details?.Technology,
					Description = details?.Description,
					Kind = relationshipKind,
					NavigateTo = details?.NavigateTo,
					Tags = relationshipTags,
					Links = details?.Links ?? [],
					Metadata = relationshipMetadata,
				}
			);
		}

		// Second pass: emit diagram-only relationships declared with WithLikeC4Reference that are
		// not backed by a ResourceRelationshipAnnotation (i.e. no WithReference was called).
		// The visited set from the first pass ensures we never duplicate a relationship.
		foreach (var details in resource.Annotations.OfType<LikeC4RelationshipDetailsAnnotation>())
		{
			var effectiveTarget = visibleByName.GetValueOrDefault(details.TargetName);
			if (effectiveTarget is null)
			{
				continue;
			}

			var targetDslId = dslIdByName.GetValueOrDefault(effectiveTarget.Name, effectiveTarget.Name);
			var key2 = (sourceDslId, targetDslId);
			if (!visited.Add(key2))
			{
				continue;
			}

			var diagramOnlyMetadata = NormaliseMetadataKeys(details.Metadata, normaliseMetadataBehaviour);

			relationships.Add(
				new LikeC4Relationship
				{
					SourceName = sourceDslId,
					TargetName = targetDslId,
					Label = details.Label,
					Technology = details.Technology,
					Description = details.Description,
					Kind = details.Kind,
					NavigateTo = details.NavigateTo,
					Tags = details.Tags,
					Links = details.Links,
					Metadata = diagramOnlyMetadata,
				}
			);
		}
	}
}
