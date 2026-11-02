using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Counts warnings with their context, then hands every message on to Unity's handler.</summary>
    internal sealed class WarningLog : ILogHandler, IDisposable
    {
        readonly ILogHandler inner;
        readonly List<string> texts = new List<string>();
        readonly List<Object> contexts = new List<Object>();

        public WarningLog()
        {
            inner = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public int Total => texts.Count;
        public IEnumerable<string> Texts => texts;

        public int Count(string text)
        {
            int count = 0;
            foreach (string logged in texts)
            {
                if (logged == text)
                {
                    count++;
                }
            }
            return count;
        }

        public Object ContextOf(string text) => contexts[texts.IndexOf(text)];

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (logType == LogType.Warning)
            {
                texts.Add(string.Format(format, args));
                contexts.Add(context);
            }
            inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context) => inner.LogException(exception, context);

        public void Dispose() => Debug.unityLogger.logHandler = inner;
    }
}
