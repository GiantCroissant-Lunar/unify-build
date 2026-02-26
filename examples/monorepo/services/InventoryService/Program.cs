using Common;
using Logging;

Logger.Info("InventoryService starting...");
var result = new Result<int>(true, 42, null);
Logger.Info($"Stock count: {result.Value}");
