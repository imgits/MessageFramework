using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace MessageFramework
{
    class Log
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <inheritdoc />
        public static void Trace(string message)
        {
            logger.Trace(message);
        }

        /// <inheritdoc />
        public static void Debug(string message)
        {
            logger.Debug(message);
        }

        /// <inheritdoc />
        public static void Fatal(string message)
        {
            logger.Fatal(message);
        }

        /// <inheritdoc />
        public static void Fatal(string message, Exception ex)
        {
            logger.Fatal(message);
        }

        /// <inheritdoc />
        public static void Info(string message)
        {
            logger.Info(message);
        }

        /// <inheritdoc />
        public static void Warn(string message)
        {
            logger.Warn(message);
        }

        /// <inheritdoc />
        public static void Error(string message)
        {
            logger.Error(message);
        }

        /// <inheritdoc />
        public static void Shutdown()
        {
            LogManager.DisableLogging();
        }

    }
}
