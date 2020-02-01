using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.Cosmos.Table;

namespace StockMarketBotDiscord.Model
{
    class PortfolioEntity : TableEntity
    {
        public PortfolioEntity()
        {

        }

        public PortfolioEntity(string userId, string name)
        {
            PartitionKey = userId;
            RowKey = name;
        }

        public string GuildId { get; set; }

        public string Stocks { get; set; }


    }
}
