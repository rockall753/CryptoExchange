﻿using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.OrderBook;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.UnitTests.TestImplementations;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CryptoExchange.Net.UnitTests
{
    [TestFixture]
    public class SymbolOrderBookTests
    {
        private static OrderBookOptions defaultOrderBookOptions = new OrderBookOptions("Test", true);

        private class TestableSymbolOrderBook : SymbolOrderBook
        {
            public TestableSymbolOrderBook() : base("BTC/USD", defaultOrderBookOptions)
            {
            }

            public override void Dispose() {}

            protected override Task<CallResult<bool>> DoResync()
            {
                throw new NotImplementedException();
            }

            protected override Task<CallResult<UpdateSubscription>> DoStart()
            {
                throw new NotImplementedException();
            }
        }

        [TestCase]
        public void GivenEmptyBidList_WhenBestBid_ThenEmptySymbolOrderBookEntry()
        {
            var symbolOrderBook = new TestableSymbolOrderBook();
            Assert.IsNotNull(symbolOrderBook.BestBid);
            Assert.AreEqual(0m, symbolOrderBook.BestBid.Price);
            Assert.AreEqual(0m, symbolOrderBook.BestAsk.Quantity);
        }

        [TestCase]
        public void GivenEmptyAskList_WhenBestAsk_ThenEmptySymbolOrderBookEntry()
        {
            var symbolOrderBook = new TestableSymbolOrderBook();
            Assert.IsNotNull(symbolOrderBook.BestBid);
            Assert.AreEqual(0m, symbolOrderBook.BestBid.Price);
            Assert.AreEqual(0m, symbolOrderBook.BestAsk.Quantity);
        }

        [TestCase]
        public void GivenEmptyBidAndAskList_WhenBestOffers_ThenEmptySymbolOrderBookEntries()
        {
            var symbolOrderBook = new TestableSymbolOrderBook();
            Assert.IsNotNull(symbolOrderBook.BestOffers);
            Assert.IsNotNull(symbolOrderBook.BestOffers.BestBid);
            Assert.IsNotNull(symbolOrderBook.BestOffers.BestAsk);
            Assert.AreEqual(0m, symbolOrderBook.BestOffers.BestBid.Price);
            Assert.AreEqual(0m, symbolOrderBook.BestOffers.BestBid.Quantity);
            Assert.AreEqual(0m, symbolOrderBook.BestOffers.BestAsk.Price);
            Assert.AreEqual(0m, symbolOrderBook.BestOffers.BestAsk.Quantity);
        }
    }
}
