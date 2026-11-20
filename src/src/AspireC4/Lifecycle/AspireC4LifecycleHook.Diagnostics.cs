using Aspire.Hosting.AspireC4.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.AspireC4.Lifecycle;

sealed partial class AspireC4LifecycleHook
{
	/// <summary>
	/// The diagram's dashboard row, recorded at startup so a background failure can be shown on its
	/// Console tab and not only in the AppHost's own log.
	/// </summary>
	volatile AspireC4Resource? _diagramResource;

	/// <summary>
	/// The diagram resource's logger, or <see langword="null"/> before startup or when the diagram has
	/// no dashboard row.
	/// </summary>
	ILogger? DiagramResourceLogger =>
		_diagramResource is { } resource && resourceLoggerService is not null
			? resourceLoggerService.GetLogger(resource)
			: null;

	/// <summary>
	/// Shows a failure on the diagram's Console tab in the dashboard.
	/// </summary>
	/// <remarks>
	/// Called beside the matching telemetry call, which still writes the AppHost log. A background task
	/// that fails is otherwise invisible to anyone looking at the dashboard: the diagram simply stops
	/// updating, and nothing on its row says why.
	/// </remarks>
	void ReportToDashboard(string message, Exception? exception = null)
	{
		var logger = DiagramResourceLogger;
		if (logger is null || !logger.IsEnabled(LogLevel.Warning))
		{
			return;
		}

		if (exception is null)
		{
			logger.Log(LogLevel.Warning, "{Message}", message);
		}
		else
		{
			logger.Log(LogLevel.Warning, exception, "{Message}: {Error}", message, exception.Message);
		}
	}

	/// <summary>
	/// Reports a background watcher that has stopped because of an unexpected exception.
	/// </summary>
	void ReportWatcherFailed(string watcher, Exception exception)
	{
		telemetry.StateWatcherFailed(watcher, exception);
		ReportToDashboard($"AspireC4 stopped watching ({watcher})", exception);
	}
}
