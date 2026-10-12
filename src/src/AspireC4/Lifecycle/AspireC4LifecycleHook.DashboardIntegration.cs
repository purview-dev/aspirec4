using System.Collections.Immutable;
using Aspire.Hosting.AspireC4.ApplicationModel;
using Aspire.Hosting.AspireC4.LikeC4;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	void SetupDashboardIntegration(
		DistributedApplicationModel appModel,
		string displayName,
		CancellationToken cancellationToken
	)
	{
		var aspirec4Resource = appModel.Resources.OfType<AspireC4Resource>().FirstOrDefault();
		var serverResource =
			aspirec4Resource?.InnerResource
			?? appModel.Resources.FirstOrDefault(r => r is LikeC4ServerResource or LikeC4LocalServerResource);

		if (serverResource is null)
		{
			return;
		}

		// Add a command annotation to every project resource so the diagram link appears
		// in the Aspire dashboard on those rows. The URL is populated once the server starts;
		// until then, the command is disabled. URL injection into snapshots is handled by
		// InjectDiagramUrlWhenLikeC4RunsAsync, which uses the correct public URL from the server's snapshot.
		foreach (var projectResource in appModel.Resources.OfType<ProjectResource>())
		{
			// ResourceCommandAnnotation: re-evaluated on every state update via UpdateCommands.
			// The executeCommand callback returns a Markdown link that the dashboard renders
			// as a clickable dialog (displayImmediately: true).
			projectResource.Annotations.Add(
				new ResourceCommandAnnotation(
					name: "likec4-architecture-diagram",
					displayName: displayName,
					updateState: _ =>
						_diagramUrl is not null ? ResourceCommandState.Enabled : ResourceCommandState.Disabled,
					executeCommand: _ =>
					{
						var url = _diagramUrl;
						return url is null
							? Task.FromResult(CommandResults.Failure("The architecture diagram is not yet available."))
							: Task.FromResult(
								CommandResults.Success(
									$"[Open {displayName}]({url})",
									new CommandResultData
									{
										Value = $"[Open {displayName}]({url})",
										Format = CommandResultFormat.Markdown,
										DisplayImmediately = true,
									}
								)
							);
					},
					displayDescription: "View the live LikeC4 architecture diagram",
					parameter: null,
					confirmationMessage: null,
					iconName: "PlugConnected",
					iconVariant: IconVariant.Filled,
					isHighlighted: false
				)
			);
		}

		// Fire-and-forget: inject diagram URL into project resources once the server is running.
		_ = InjectDiagramUrlWhenLikeC4RunsAsync(appModel, serverResource, displayName, cancellationToken);
	}

	/// <summary>
	/// Watches for the LikeC4 server resource to become Running, then directly injects
	/// its URL into each project resource's snapshot so the link appears immediately —
	/// even when the server starts after the project resource's endpoints are already allocated.
	/// Also caches the URL into <see cref="_diagramUrl"/> so the dashboard command handler
	/// can use the correct public URL (with scheme, public port, and path).
	/// </summary>
	async Task InjectDiagramUrlWhenLikeC4RunsAsync(
		DistributedApplicationModel appModel,
		IResource serverResource,
		string displayName,
		CancellationToken cancellationToken
	)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != serverResource.Name)
					continue;
				if (notification.Snapshot.State?.Text != KnownResourceStates.Running)
					continue;

				var url = notification
					.Snapshot.Urls.FirstOrDefault(u => u.Name == AspireC4Resource.HttpEndpointName && !u.IsInternal)
					?.Url;

				if (url is null)
					continue;

				// Cache the URL so the command handler can use the correct public URL.
				_diagramUrl = url;

				var capturedUrl = url;
				foreach (var resource in appModel.Resources.OfType<ProjectResource>())
				{
					await resourceNotificationService.PublishUpdateAsync(
						resource,
						s =>
							// Avoid duplicates on repeated Running notifications.
							s.Urls.Any(u => u.Name == "architecture-diagram")
								? s
								: (
									s with
									{
										Urls = s.Urls.Add(
											new UrlSnapshot(
												Name: "architecture-diagram",
												Url: capturedUrl,
												IsInternal: false
											)
											{
												DisplayProperties = new UrlDisplayPropertiesSnapshot(displayName),
											}
										),
									}
								)
					);
				}

				break; // Only inject once — the URL persists in the snapshot.
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Continuously re-publishes <c>IsHidden = true</c> on the LikeC4 server resource
	/// whenever DCP resets it to visible, ensuring it stays off the dashboard resource list.
	/// </summary>
	async Task KeepServerHiddenAsync(IResource serverResource, CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != serverResource.Name)
					continue;
				if (notification.Snapshot.IsHidden)
					continue;

				await resourceNotificationService.PublishUpdateAsync(serverResource, s => s with { IsHidden = true });
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Watches the inner resource's log stream and relays every log line to the outer
	/// <see cref="AspireC4Resource"/> so that the Console tab in the Aspire dashboard shows
	/// the LikeC4 server output under the <c>aspirec4</c> entry rather than the hidden inner
	/// resource.
	/// </summary>
	async Task ForwardInnerResourceLogsAsync(AspireC4Resource outerResource, CancellationToken cancellationToken)
	{
		var innerResource = outerResource.InnerResource;
		if (innerResource is null)
			return;

		try
		{
			var outerLogger = resourceLoggerService.GetLogger(outerResource);

			await foreach (
				var logBatch in resourceLoggerService.WatchAsync(innerResource).WithCancellation(cancellationToken)
			)
			{
				foreach (var logLine in logBatch)
				{
					var level = logLine.IsErrorMessage ? LogLevel.Error : LogLevel.Information;
					if (outerLogger.IsEnabled(level))
					{
						outerLogger.Log(level, "{Content}", logLine.Content);
					}
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	/// <summary>
	/// Watches for state changes on the inner resource and forwards them to the outer
	/// <see cref="AspireC4Resource"/> so consumers watching the outer resource name
	/// (e.g., integration tests using <c>ResourceNotifications</c>) receive the correct lifecycle
	/// state, URLs (e.g. the diagram link), and properties (e.g. port information).
	/// This makes the outer <see cref="AspireC4Resource"/> the single useful dashboard entry —
	/// the inner server resource is always kept hidden.
	/// </summary>
	/// <summary>Name of the "LikeC4 Version" property shown on the diagram's dashboard row.</summary>
	const string LikeC4VersionProperty = "LikeC4 Version";

	/// <summary>
	/// Adds or updates the LikeC4 version on a snapshot's properties, leaving every other property in
	/// place.
	/// </summary>
	/// <remarks>
	/// Replacing the collection instead would discard what Aspire itself puts there — in particular
	/// <c>resource.parentName</c>, which is what the dashboard builds its resource tree from. Dropping
	/// it leaves the <see cref="ResourceRelationshipAnnotation"/> intact, so the application model and
	/// every unit test still look correct while the row quietly unnests itself in the dashboard.
	/// </remarks>
	ImmutableArray<ResourcePropertySnapshot> WithLikeC4Version(ImmutableArray<ResourcePropertySnapshot> properties)
	{
		if (_resolvedLikeC4Version is not { } version)
		{
			return properties;
		}

		ResourcePropertySnapshot property = new(LikeC4VersionProperty, version);

		// Forwarding runs on every notification from the inner resource, so this has to be an upsert:
		// appending unconditionally would grow the row a duplicate property per state change.
		for (var i = 0; i < properties.Length; i++)
		{
			if (properties[i].Name.Equals(LikeC4VersionProperty, StringComparison.Ordinal))
			{
				return properties.SetItem(i, property);
			}
		}

		return properties.Add(property);
	}

	async Task ForwardInnerResourceStateAsync(AspireC4Resource outerResource, CancellationToken cancellationToken)
	{
		var innerResource = outerResource.InnerResource;
		if (innerResource is null)
			return;

		try
		{
			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				if (notification.Resource.Name != innerResource.Name)
					continue;

				await resourceNotificationService.PublishUpdateAsync(
					outerResource,
					s =>
						s with
						{
							State = notification.Snapshot.State,
							Urls = notification.Snapshot.Urls,
							Properties = WithLikeC4Version(s.Properties),
						}
				);
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on shutdown.
		}
#pragma warning disable CA1031
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	async Task WatchResourceStatesAsync(
		DistributedApplicationModel appModel,
		IReadOnlySet<Type>? excludedResourceTypes,
		CancellationToken cancellationToken
	)
	{
		try
		{
			var visibleNames = ModelBuilder.GetVisibleResourceNames([.. appModel.Resources], excludedResourceTypes);

			await foreach (var notification in resourceNotificationService.WatchAsync(cancellationToken))
			{
				// Ignore resources that aren't shown in the diagram.
				if (!visibleNames.Contains(notification.Resource.Name))
				{
					continue;
				}

				// Always capture external URLs from the snapshot (updates on every notification
				// so that once endpoints are allocated the correct public-port URLs are stored).
				var externalUrls = notification
					.Snapshot.Urls.Where(u =>
						!u.IsInternal
						&& (
							u.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
							|| u.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
						)
					)
					.Select(u => (u.Url, Name: u.Name ?? "endpoint"))
					.ToImmutableArray();

				if (!externalUrls.IsEmpty)
				{
					_resourceExternalUrls[notification.Resource.Name] = externalUrls;
				}

				var newState = MapAspireState(notification.Snapshot);

				if (_resourceStates.TryGetValue(notification.Resource.Name, out var current) && current == newState)
				{
					continue;
				}

				_resourceStates[notification.Resource.Name] = newState;
				telemetry.ResourceStateChanged(notification.Resource.Name, newState ?? string.Empty);

				ScheduleRegeneration(appModel, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			// Normal on application shutdown.
		}
#pragma warning disable CA1031 // intentional: watcher must not crash the host process
		catch (Exception ex)
		{
			telemetry.StateWatcherFailed(ex.Message);
		}
#pragma warning restore CA1031
	}

	void ScheduleRegeneration(DistributedApplicationModel appModel, CancellationToken cancellationToken)
	{
		CancellationTokenSource newCts;
		lock (_debounceLock)
		{
			_debounceCts?.Cancel();
			_debounceCts?.Dispose();
			newCts = _debounceCts = new CancellationTokenSource();
		}

		_ = Task.Run(
			async () =>
			{
				try
				{
					await Task.Delay(TimeSpan.FromMilliseconds(300), newCts.Token);
					telemetry.RegeneratingDiagramDueToStateChange();
					await WriteC4FileAsync(appModel, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					// Debounced — a newer state change superseded this one.
				}
			},
			CancellationToken.None
		);
	}
}
