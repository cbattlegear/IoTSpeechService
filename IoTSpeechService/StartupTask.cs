using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using System.Threading.Tasks;

using Windows.Media.SpeechSynthesis;

using Restup.Webserver.Rest;
using Restup.Webserver.Http;
using Restup.Webserver.Attributes;
using Restup.Webserver.Models.Schemas;
using Restup.Webserver.Models.Contracts;
using Windows.UI.Xaml.Controls;
using Windows.System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace IoTSpeechService
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // This deferral should have an instance reference, if it doesn't... the GC will
            // come some day, see that this method is not active anymore and the local variable
            // should be removed. Which results in the application being closed.
            _deferral = taskInstance.GetDeferral();
            var restRouteHandler = new RestRouteHandler();
            restRouteHandler.RegisterController<ParameterController>();

            var configuration = new HttpServerConfiguration()
              .ListenOnPort(8800)
              .RegisterRoute("api", restRouteHandler)
              .EnableCors();

            var httpServer = new HttpServer(configuration);
            await httpServer.StartServerAsync();
        }

        private class DataReceived
        {
            public string ToSay { get; set; }
        }

        [RestController(InstanceCreationType.Singleton)]
        private class ParameterController
        {
            [UriFormat("/speak")]
            public IPostResponse speak_words([FromContent]DataReceived data)
            {
                Speak(data.ToSay);
                return new PostResponse(PostResponse.ResponseStatus.Created);
            }
        }

        // Speak the text
        private static async void Speak(string text)
        {
            await Task.Run(() => _Speak(text));
        }

        // Internal speak method
        private static async void _Speak(string text)
        {
            MediaPlayer mediaElement = new MediaPlayer();
            SpeechSynthesizer synth = new SpeechSynthesizer();

            //Get audio devices
            string audioSelector = MediaDevice.GetAudioRenderSelector();
            var outputDevices = await DeviceInformation.FindAllAsync(audioSelector);

            foreach (var device in outputDevices)
            {
                System.Diagnostics.Debug.WriteLine(device.Name);
            }

            VoiceInformation voiceInfo =
            (
                from voice in SpeechSynthesizer.AllVoices
                where voice.Gender == VoiceGender.Female
                select voice
            ).FirstOrDefault() ?? SpeechSynthesizer.DefaultVoice;

            synth.Voice = voiceInfo;

            // Initialize a new instance of the SpeechSynthesizer.
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);

            // Send the stream to the media object.
            mediaElement.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            mediaElement.AutoPlay = true;
            mediaElement.Play();
            synth.Dispose();
            mediaElement.Dispose();
        }
    }
}
