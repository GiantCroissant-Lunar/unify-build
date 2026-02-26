using Common;
using Logging;

Logger.Info("OrderService starting...");
var result = new Result<string>(true, "Order created", null);
Logger.Info($"Result: {result.Success}");
