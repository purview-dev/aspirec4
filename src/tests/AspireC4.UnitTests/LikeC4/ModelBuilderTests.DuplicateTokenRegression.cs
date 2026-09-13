using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;

namespace Aspire.Hosting.AspireC4;

partial class ModelBuilderTests
{
	[Test]
	public async Task Build_NodeAppResource_FullNamespace_InfersNodejsIcon()
	{
		// Arrange
		// Regression: Aspire.Hosting.JavaScript.NodeAppResource tokenises as
		// "aspire" + "hosting" + "javascript"→"node" + "node" + "app" + "resource".
		// After stop-token removal that gave ["node", "node"] — duplicate tokens inflated
		// the node-sass score above nodejs.  After .Distinct(), query is ["node"] → nodejs wins.
		NodeAppResource resource = new("my-node-app");

		// Act
		var model = ModelBuilder.Build([resource]);

		// Assert
		await Assert.That(model.Elements[0].Icon).IsEqualTo("tech:nodejs");
	}

	static ProjectResource CreateProjectResource(string name)
	{
		ProjectResource resource = new(name);
		return resource;
	}

	static ContainerResource CreateContainerResource(string name)
	{
		ContainerResource resource = new(name);
		return resource;
	}

	sealed class TestDatabaseResource(string name) : Resource(name), IResourceWithConnectionString
	{
		public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"host=localhost");
	}

	sealed class TestSystemResource(string name) : Resource(name);

	sealed class TestChildResource(string name, IResource parent) : Resource(name), IResourceWithParent
	{
		public IResource Parent { get; } = parent;
	}

	// Named to match real Aspire Azure resource class names so the icon matcher
	// receives the correct type tokens via the hidden-original lookup.
	sealed class AzurePostgresFlexibleServerResource(string name) : Resource(name);

	sealed class AzureManagedRedisResource(string name) : Resource(name);

	// Named to match the real Aspire.Hosting.JavaScript.JavaScriptInstallerResource so that
	// the icon matcher sees "JavaScript" in the type's short name and triggers the bigram fix.
	sealed class JavaScriptInstallerResource(string name) : Resource(name);

	// Generic Java app — "java" appears without a following "script" token, so tech:java
	// should still be inferred.
	sealed class TestJavaAppResource(string name) : Resource(name);
}
