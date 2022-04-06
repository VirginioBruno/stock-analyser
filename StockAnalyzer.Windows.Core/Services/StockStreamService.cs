using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using StockAnalyzer.Core.Domain;

namespace StockAnalyzer.Windows.Services
{
    public interface IStockStreamService
    {
        IAsyncEnumerable<StockPrice> GetAllStockPrices(CancellationToken cancellationToken = default);
    }

    public class MockStockStreamService : IStockStreamService
    {
        public async IAsyncEnumerable<StockPrice> GetAllStockPrices([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(500, cancellationToken);

            yield return new StockPrice() { Identifier = "MSFT", Change = 10.9m };

            await Task.Delay(500, cancellationToken);

            yield return new StockPrice() { Identifier = "MSFT", Change = 5.4m };

            await Task.Delay(500, cancellationToken);

            yield return new StockPrice() { Identifier = "MSFT", Change = 11.8m };

            await Task.Delay(500, cancellationToken);

            yield return new StockPrice() { Identifier = "MSFT", Change = 36.2m };

            await Task.Delay(500, cancellationToken);

            yield return new StockPrice() { Identifier = "MSFT", Change = 4.4m };
        }
    }

    public class DiskStockStreamService : IStockStreamService
    {
        public async IAsyncEnumerable<StockPrice> GetAllStockPrices([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var stream = new StreamReader(File.OpenRead("StockPrices_Small.csv"));
            await stream.ReadLineAsync(); //read header

            string line;
            while ((line = await stream.ReadLineAsync()) != null) 
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                yield return StockPrice.FromCSV(line);
            }
        }
    }
}
