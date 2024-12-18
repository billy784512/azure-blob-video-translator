using App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace App.Factories
{
    public class VideoProcessingStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly VideoProcessingService _videoService;

        public VideoProcessingStrategyFactory(IServiceProvider serviceProvider, VideoProcessingService videoService){
            _serviceProvider = serviceProvider;
            _videoService = videoService;
        }

        public BaseVideoProcessingStrategy GetStrategy(string mode)
        {
            return mode.ToLower() switch
            {
                "simple" => _serviceProvider.GetRequiredService<SimpleVideoProcessingStrategy>(),
                // "advanced" => new AdvancedVideoProcessingStrategy(_videoService)
                // "complex" => new ComplexVideoProcessingStrategy(_videoService)
                _ => throw new ArgumentException($"Unknown mode: {mode}")
            };
        }
    }
}