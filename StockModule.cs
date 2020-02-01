using Discord.Commands;
using Microsoft.Azure.Cosmos.Table;
using StockMarketBot.Model;
using StockMarketBot.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YahooFinanceApi;

namespace StockMarketBot
{
    public class StockModule : ModuleBase<SocketCommandContext>
    {
        CloudTable table;
        public StockModule()
        {
            table = Common.CreateTableAsync("StockMarketBotContainer");
        }

        [Command("help")]
        [Summary("Displays commands")]
        public async Task HelpAsync()
        {
            await ReplyAsync("Type 'smb! ' followed by any of the listed commands:\n" +
                "price [stock symbols] - Lists current prices for a list of stock prices. ex: smb! price AAPL TSLA GOOGL \n" +
                "new portfolio [name] - Creates a new protfolio. \n"+
                "add to [portfolio name] [stock symbol] [shares] - Adds an amount of shares of stock at current market price to portfolio. \n" +
                "portfolio [name] - Lists out portfolio holdings."
                );
        }


        [Command("price")]
        [Summary("Gets stock price.")]
        public async Task PriceAsync([Remainder] [Summary("Stock symbol")] string symbols)
        {
            string[] symbolList = symbols.Split(' ');
            var prices = await Yahoo.Symbols(symbolList).Fields(Field.RegularMarketPrice, Field.RegularMarketChange, Field.RegularMarketChangePercent).QueryAsync();
            if (prices.Count == 0) await ReplyAsync("No stocks found with that symbol.");
            foreach (var s in symbolList.ToList().Take(15))
            {
                string plus = "";
                if (prices[s].RegularMarketChangePercent > 0)
                {
                    plus = "+";
                }
                await ReplyAsync(s + ": "
                    + prices[s].RegularMarketPrice + " " + plus
                    + String.Format("{0:0.0000}", prices[s].RegularMarketChange).TrimEnd('0') +
                    " (" + plus + String.Format("{0:0.00}", prices[s].RegularMarketChangePercent) + "%)");
            }

        }

        [Command("new portfolio")]
        [Summary("Adds portfolio to account")]
        public async Task NewPortfolioAsync([Remainder] [Summary("Protfolio name")] string name)
        {

            PortfolioEntity portfolio = new PortfolioEntity(Context.User.Id.ToString(), name);
            portfolio.GuildId = Context.Guild.Id.ToString();
            portfolio.Stocks = string.Empty;
            await CloudTableUtility.InsertOrMergePortfolioAsync(table, portfolio);
            await ReplyAsync("New portfolio '" + name + "' added!");
        }

        [Command("add to")]
        [Summary("Adds symbol to protfolio")]
        public async Task AddToPortfolio([Remainder] [Summary("Portfolio name and symbol")] string portfolioAndSymbol)
        {

            string[] pasList = portfolioAndSymbol.Split(' ');
            if (pasList.Count() < 2)
            {
                if (pasList.Count() == 1)
                {
                    await ReplyAsync("No symbol defined. \n" + "Format: smb! add to [protfolio name] [symbol] [shares]");
                }
                else
                {
                    await ReplyAsync("Invalid parameters. \n" + "Format: smb! add to [protfolio name] [symbol] [shares]");
                }
            }
            else
            {
                PortfolioEntity portfolio = await CloudTableUtility.RetrievePortfolioUsingPointQueryAsync(table, Context.User.Id.ToString(), pasList[0]);
                if (portfolio == null)
                {
                    await ReplyAsync("No portfolio with that name.");
                }
                string[] symbols = new string[pasList.Length - 1];
                Array.Copy(pasList, 1, symbols, 0, pasList.Count() - 1);
                var prices = await Yahoo.Symbols(symbols).Fields(Field.RegularMarketPrice).QueryAsync();
                StockEntity stock;
                List<StockEntity> newStocks = new List<StockEntity>();
                for (int i = 0; i < symbols.Length - 1; i++)
                {
                    if (i % 2 == 0)
                    {

                        int shares = 0;
                        int.TryParse(symbols[i + 1], out shares);
                        if (shares > 0)
                        {
                            stock = new StockEntity(portfolio.RowKey, symbols[i]);
                            stock.Shares = shares;
                            stock.StartPrice = prices[symbols[i]].RegularMarketPrice;
                            newStocks.Add(stock);

                        }
                        else
                        {

                            await ReplyAsync("Error adding holdings to portfolio.");
                            return;
                        }


                    }
                }
                foreach (var s in newStocks)
                {
                    if (portfolio.Stocks.Split(' ').Contains(s.RowKey))
                    {
                        StockEntity stockEntity = await CloudTableUtility.RetrieveStockUsingPointQueryAsync(table, portfolio.RowKey, s.RowKey);

                        var newAddedCost = s.Shares * s.StartPrice;
                        var oldCost = stockEntity.Shares * stockEntity.StartPrice;
                        var totalShares = s.Shares + stockEntity.Shares;
                        var startPrice = Math.Round((newAddedCost + oldCost) / totalShares,2);

                        stockEntity.Shares = totalShares;
                        stockEntity.StartPrice = startPrice;

                        await CloudTableUtility.InsertOrMergeStockAsync(table, stockEntity);
                    }
                    else
                    {
                        await CloudTableUtility.InsertOrMergeStockAsync(table, s);
                        portfolio.Stocks += (" " + s.RowKey + " ").Trim();
                    }


                }

                await CloudTableUtility.InsertOrMergePortfolioAsync(table, portfolio);

                await ReplyAsync("Holdings added to " + portfolio.RowKey);

            }



        }

        [Command("portfolio")]
        public async Task GetPortfolio([Remainder][Summary("Portfolio name")] string name)
        {

            PortfolioEntity portfolio = await CloudTableUtility.RetrievePortfolioUsingPointQueryAsync(table, Context.User.Id.ToString(), name);
            await ReplyAsync("Portfolio: " + portfolio.RowKey);
            string returnString = "";
            StockEntity stock;
            List<StockEntity> stockList = new List<StockEntity>();
            var symbolList = portfolio.Stocks.Trim().Split(" ");
            var newPriceList = await Yahoo.Symbols(symbolList).Fields(Field.RegularMarketPrice).QueryAsync();
            double totalValue = 0;
            double totalCost = 0;
            double totalTotalChange = 0;
            double totalChangePercent = 0;
            foreach (var s in symbolList)
            {
                stock = await CloudTableUtility.RetrieveStockUsingPointQueryAsync(table, portfolio.RowKey, s);

                var newHoldingValue = stock.Shares * newPriceList[s].RegularMarketPrice;
                var oldHoldingValue = stock.Shares * stock.StartPrice;
                var changeInValue = Math.Round((((newHoldingValue - oldHoldingValue) / oldHoldingValue * 100)), 2);
                var totalChange = newHoldingValue - oldHoldingValue;

                totalValue = totalValue + newHoldingValue;
                totalCost = totalCost + oldHoldingValue;
                totalTotalChange = Math.Round(totalTotalChange + totalChange,2);


                returnString += s + "   " + newHoldingValue + "(" + oldHoldingValue + ")   " + newPriceList[s].RegularMarketPrice +"("+Math.Round(stock.StartPrice,2) +")" + "   " + totalChange + "(" + changeInValue + "%)" + "\n";
            }
            totalChangePercent = Math.Round((((totalValue - totalCost) / totalCost * 100)), 2);
            await ReplyAsync("TICKER  VALUE(COST)  PRICE(AVG. COST)  TOTAL(%): \n" + returnString + "\n" +
            "Totals:\n" +
            "VALUE(COST)  TOTAL(%)\n" +
            totalValue + "(" + totalCost + ")" + "   " + totalTotalChange + "(" + totalChangePercent + "%)" + "\n");

        }



    }
}
