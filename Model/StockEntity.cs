using Microsoft.Azure.Cosmos.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace StockMarketBotDiscord.Model
{
    class StockEntity : TableEntity
    {
        public StockEntity()
        {

        }
        public StockEntity(string portfolioId, string symbol)
        {
            PartitionKey = portfolioId;
            RowKey = symbol;
        }

        public double StartPrice { get; set; }
        public int Shares { get; set; }
    }
}
