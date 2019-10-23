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
using System.IO;
using Newtonsoft.Json;
using System.Globalization;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace IoTSpeechService
{
    public sealed class StartupTask : IBackgroundTask
    {
        private static readonly HttpClient client = new HttpClient();

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

        private class MistyData
        {
            public string FileName { get; set; }
            public string Data { get; set; }
            public bool ImmediatelyApply { get; set; }
            public bool OverwriteExisting { get; set; }
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
            SpeechSynthesizer synth = new SpeechSynthesizer();

            VoiceInformation voiceInfo =
            (
                from voice in SpeechSynthesizer.AllVoices
                where voice.Gender == VoiceGender.Female
                select voice
            ).FirstOrDefault() ?? SpeechSynthesizer.DefaultVoice;

            synth.Voice = voiceInfo;

            // Initialize a new instance of the SpeechSynthesizer.
            SpeechSynthesisStream stream = await synth.SynthesizeTextToStreamAsync(text);
            /*Stream holdStream = stream.AsStream();
            MemoryStream speechStream = new MemoryStream();

            holdStream.CopyTo(speechStream);

            byte[] soundBytes = speechStream.ToArray();

            string soundString = Convert.ToBase64String(soundBytes);

            MistyData data = new MistyData();
            data.FileName = "Response.wav";
            data.Data = soundString;
            data.OverwriteExisting = true;
            data.ImmediatelyApply = true; */

            try
            {
                using(var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    content.Add(new StreamContent(stream.AsStream()));
                    var message = await client.PostAsync("http://10.10.10.10/api/audio", content);
                }

            } catch (HttpRequestException e)
            {
                System.Diagnostics.Debug.WriteLine(e.InnerException.Message);
            }

            // Send the stream to the media object.
            //mediaElement.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            //mediaElement.AutoPlay = true;
            //mediaElement.Play();
            synth.Dispose();
            //mediaElement.Dispose();
        }
    }
}
