using Aspire.Hosting.AspireC4.EntityFrameworkCore.Models;

namespace Aspire.Hosting.AspireC4.EntityFrameworkCore;

/// <summary>
/// What a table style callback is given: the table, where it sits in the model, and the style the
/// matching rules produced for it, which the callback can change.
/// </summary>
/// <remarks>
/// Callbacks run each time the diagram is regenerated — on startup and whenever a contributing project
/// restarts with a changed model — so a style computed from the model, such as colouring a table by its
/// column count, follows the model as it changes. They can run more than once per regeneration (the
/// image export renders separately), so keep them free of side effects.
/// </remarks>
public sealed class ErdTableStyleContext
{
	internal ErdTableStyleContext(EfModel model, EfContext dbContext, EfTable table, ErdElementStyle style)
	{
		Model = model;
		DbContext = dbContext;
		Table = table;
		Style = style;
	}

	/// <summary>The whole model being drawn.</summary>
	public EfModel Model { get; }

	/// <summary>The <c>DbContext</c> the table belongs to.</summary>
	public EfContext DbContext { get; }

	/// <summary>The table being styled.</summary>
	public EfTable Table { get; }

	/// <summary>The table's style, already holding whatever the matching rules set. Change it in place.</summary>
	public ErdElementStyle Style { get; }
}

/// <summary>
/// What a column style callback is given: the column, its table, and the style the matching rules
/// produced for it, which the callback can change.
/// </summary>
/// <remarks><inheritdoc cref="ErdTableStyleContext" path="/remarks"/></remarks>
public sealed class ErdColumnStyleContext
{
	internal ErdColumnStyleContext(
		EfModel model,
		EfContext dbContext,
		EfTable table,
		EfColumn column,
		ErdElementStyle style
	)
	{
		Model = model;
		DbContext = dbContext;
		Table = table;
		Column = column;
		Style = style;
	}

	/// <summary>The whole model being drawn.</summary>
	public EfModel Model { get; }

	/// <summary>The <c>DbContext</c> the column's table belongs to.</summary>
	public EfContext DbContext { get; }

	/// <summary>The table the column belongs to.</summary>
	public EfTable Table { get; }

	/// <summary>The column being styled.</summary>
	public EfColumn Column { get; }

	/// <summary>The column's style, already holding whatever the matching rules set. Change it in place.</summary>
	public ErdElementStyle Style { get; }
}

/// <summary>
/// What a relationship style callback is given: the foreign key, the tables at each end, and the style
/// the matching rules produced for it, which the callback can change.
/// </summary>
/// <remarks><inheritdoc cref="ErdTableStyleContext" path="/remarks"/></remarks>
public sealed class ErdRelationshipStyleContext
{
	internal ErdRelationshipStyleContext(
		EfModel model,
		EfContext dbContext,
		EfTable dependent,
		EfTable principal,
		EfForeignKey foreignKey,
		ErdElementStyle style
	)
	{
		Model = model;
		DbContext = dbContext;
		Dependent = dependent;
		Principal = principal;
		ForeignKey = foreignKey;
		Style = style;
	}

	/// <summary>The whole model being drawn.</summary>
	public EfModel Model { get; }

	/// <summary>The <c>DbContext</c> the dependent table belongs to.</summary>
	public EfContext DbContext { get; }

	/// <summary>The table holding the foreign key.</summary>
	public EfTable Dependent { get; }

	/// <summary>The table the foreign key references.</summary>
	public EfTable Principal { get; }

	/// <summary>The foreign key behind the relationship.</summary>
	public EfForeignKey ForeignKey { get; }

	/// <summary>
	/// The relationship's style, already holding whatever the matching rules set. Change it in place.
	/// </summary>
	public ErdElementStyle Style { get; }
}
