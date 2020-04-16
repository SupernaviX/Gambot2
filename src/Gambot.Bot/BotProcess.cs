using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gambot.Core;
using Gambot.IO;
using Gambot.Module.Say;
using SimpleInjector;

namespace Gambot.Bot
{
    public class BotProcess
    {
        private readonly ICollection<IListener> _listeners;
        private readonly ICollection<IResponder> _responders;
        private readonly ICollection<ITransformer> _transformers;

        private readonly IMessenger _messenger;

        private readonly ILogger _log;

        public BotProcess(ICollection<IListener> listeners, ICollection<IResponder> responders, ICollection<ITransformer> transformers, IMessenger messenger, ILogger log)
        {
            _listeners = listeners;
            _responders = responders;
            _transformers = transformers;
            _messenger = messenger;
            _log = log;
        }

        public async Task Initialize()
        {
            _log.Info("Initializing bot process.");
            var connected = await _messenger.Connect();
            if (!connected)
            {
                _log.Warn("Unable to connect to messenger.");
                return;
            }
            _log.Trace("Connected.");

            _messenger.OnMessageReceived += HandleMessage;
        }

        public async void HandleMessage(object sender, OnMessageReceivedEventArgs e)
        {
            var message = e.Message;

            _log.Trace("Processing listeners");
            await Task.WhenAll(_listeners.Select(async l => await l.Listen(message)));
            _log.Trace("Listeners have listened.");

            _log.Trace("Processing responders");
            Response response = null;
            foreach (var responder in _responders)
            {
                response = await responder.Respond(message);
                if (response != null)
                {
                    _log.Trace("Got a response");
                    break;
                }
            }
            _log.Trace("Responders have responded.");

            if (response == null)
            {
                _log.Debug("No response generated.");
                return;
            }

            _log.Trace("Processing transformers");
            foreach (var transformer in _transformers)
            {
                response = await transformer.Transform(response);
            }
            _log.Trace("Transformers have transformed");

            _log.Trace("Sending response");
            await response.Send();
        }
    }
}