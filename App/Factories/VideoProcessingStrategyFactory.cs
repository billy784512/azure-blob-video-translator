using Microsoft.Extensions.DependencyInjection;

namespace App.Factories
{
    public class VideoProcessingStrategyFactory
    {
        private readonly Dictionary<string, Func<BaseVideoProcessingStrategy>> _strategyFactories;

        public VideoProcessingStrategyFactory(IServiceProvider serviceProvider)
        {
            _strategyFactories = new Dictionary<string, Func<BaseVideoProcessingStrategy>>()
            {
                { "simple", () => serviceProvider.GetRequiredService<SimpleVideoProcessingStrategy>() }
                //{ "advanced", () => serviceProvider.GetRequiredService<AdvancedVideoProcessingStrategy>() }
            };
        }

        public BaseVideoProcessingStrategy GetStrategy(string mode)
        {
            if (_strategyFactories.TryGetValue(mode, out var factory))
            {
                return factory();
            }

            throw new ArgumentException($"Unknown mode: {mode}");
        }
    }
}