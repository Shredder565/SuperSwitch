using SuperSwitch.Audio.Renderer.Parameter;

namespace SuperSwitch.Horizon.Sdk.Audio.Detail
{
    struct AudioRendererParameterInternal
    {
        public AudioRendererConfiguration Configuration;

        public AudioRendererParameterInternal(AudioRendererConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
