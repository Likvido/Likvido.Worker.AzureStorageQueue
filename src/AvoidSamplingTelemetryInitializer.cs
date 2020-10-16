using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Likvido.Worker.AzureStorageQueue
{
    public class AvoidSamplingTelemetryInitializer : ITelemetryInitializer
    {
        private readonly Func<ITelemetry, bool> _condition;

        public AvoidSamplingTelemetryInitializer(Func<ITelemetry, bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        public void Initialize(ITelemetry telemetry)
        {
            //https://stackoverflow.com/a/60951047/1643576
            if (telemetry is ISupportSampling supportSampling
                &&_condition(telemetry))
            {
                supportSampling.SamplingPercentage = 100;
            }
        }
    }
}
