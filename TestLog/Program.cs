using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
namespace TestLog
{
    class Program
    {
        static void Main(string[] args)
        {
			var logger = LogManager.GetCurrentClassLogger();

			logger.Trace("Hello!");
			logger.Debug("This is");
			logger.Info("NLog using");
			logger.Warn("Append blobs in");
			logger.Error("Windows Azure");
			logger.Fatal("Storage.");

			try
			{
				throw new NotSupportedException();
			}
			catch (Exception ex)
			{
				logger.Error( ex);
			}
			Thread.Sleep(20000);
		}
    }
}
