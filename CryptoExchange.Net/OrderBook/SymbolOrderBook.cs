﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;

namespace CryptoExchange.Net.OrderBook
{
    public abstract class SymbolOrderBook: IDisposable
    {
        protected readonly List<ProcessBufferEntry> processBuffer;
        private readonly object bookLock = new object();
        protected List<OrderBookEntry> asks;
        protected List<OrderBookEntry> bids;
        private OrderBookStatus status;
        private UpdateSubscription subscription;
        private readonly bool sequencesAreConsecutive;
        private readonly string id;
        protected Log log;

        private bool bookSet;

        /// <summary>
        /// The status of the order book. Order book is up to date when the status is `Synced`
        /// </summary>
        public OrderBookStatus Status
        {
            get => status;
            set
            {
                if (value == status)
                    return;

                var old = status;
                status = value;
                log.Write(LogVerbosity.Info, $"{id} order book {Symbol} status changed: {old} => {value}");
                OnStatusChange?.Invoke(old, status);
            }
        }

        /// <summary>
        /// Last update identifier
        /// </summary>
        public long LastSequenceNumber { get; private set; }
        /// <summary>
        /// The symbol of the order book
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// Event when the state changes
        /// </summary>
        public event Action<OrderBookStatus, OrderBookStatus> OnStatusChange;

        /// <summary>
        /// The number of asks in the book
        /// </summary>
        public int AskCount { get; private set; }
        /// <summary>
        /// The number of bids in the book
        /// </summary>
        public int BidCount { get; private set; }

        /// <summary>
        /// The list of asks
        /// </summary>
        public IEnumerable<ISymbolOrderBookEntry> Asks
        {
            get
            {
                lock (bookLock)
                    return asks.OrderBy(a => a.Price).ToList();
            }
        }

        /// <summary>
        /// The list of bids
        /// </summary>
        public IEnumerable<ISymbolOrderBookEntry> Bids
        {
            get
            {
                lock (bookLock)
                    return bids.OrderByDescending(a => a.Price).ToList();
            }
        }

        protected SymbolOrderBook(string id, string symbol, bool sequencesAreConsecutive, LogVerbosity logVerbosity, IEnumerable<TextWriter> logWriters)
        {
            this.id = id;
            processBuffer = new List<ProcessBufferEntry>();
            this.sequencesAreConsecutive = sequencesAreConsecutive;
            Symbol = symbol;
            Status = OrderBookStatus.Disconnected;

            asks = new List<OrderBookEntry>();
            bids = new List<OrderBookEntry>();

            log = new Log { Level = logVerbosity };
            if (logWriters == null)
                logWriters = new List<TextWriter> { new DebugTextWriter() };
            log.UpdateWriters(logWriters.ToList());
        }

        /// <summary>
        /// Start connecting and synchronizing the order book
        /// </summary>
        /// <returns></returns>
        public async Task<CallResult<bool>> Start()
        {
            Status = OrderBookStatus.Connecting;
            var startResult = await DoStart().ConfigureAwait(false);
            if(!startResult.Success)
                return new CallResult<bool>(false, startResult.Error);

            subscription = startResult.Data;
            subscription.ConnectionLost += Reset;
            subscription.ConnectionRestored += (time) => Resync();
            Status = OrderBookStatus.Synced;
            return new CallResult<bool>(true, null);
        }

        private void Reset()
        {
            log.Write(LogVerbosity.Warning, $"{id} order book {Symbol} connection lost");
            Status = OrderBookStatus.Connecting;
            processBuffer.Clear();
            bookSet = false;
            DoReset();
        }

        private void Resync()
        {
            Status = OrderBookStatus.Syncing;
            bool success = false;
            while (!success)
            {
                if (Status != OrderBookStatus.Syncing)
                    return;

                var resyncResult = DoResync().Result;
                success = resyncResult.Success;
            }

            log.Write(LogVerbosity.Info, $"{id} order book {Symbol} successfully resynchronized");
            Status = OrderBookStatus.Synced;
        }

        /// <summary>
        /// Stop syncing the order book
        /// </summary>
        /// <returns></returns>
        public Task Stop()
        {
            Status = OrderBookStatus.Disconnected;
            return subscription.Close();
        }

        protected abstract Task<CallResult<UpdateSubscription>> DoStart();

        protected virtual void DoReset() { }

        protected abstract Task<CallResult<bool>> DoResync();
        
        protected void SetInitialOrderBook(long orderBookSequenceNumber, IEnumerable<ISymbolOrderBookEntry> askList, IEnumerable<ISymbolOrderBookEntry> bidList)
        {
            lock (bookLock)
            {
                if (Status == OrderBookStatus.Connecting)
                    return;

                asks = askList.Select(a => new OrderBookEntry(a.Price, a.Quantity)).ToList();
                bids = bidList.Select(b => new OrderBookEntry(b.Price, b.Quantity)).ToList();
                LastSequenceNumber = orderBookSequenceNumber;

                AskCount = asks.Count;
                BidCount = asks.Count;

                CheckProcessBuffer();
                bookSet = true;
                log.Write(LogVerbosity.Debug, $"{id} order book {Symbol} initial order book set");
            }
        }

        protected void UpdateOrderBook(long firstSequenceNumber, long lastSequenceNumber, List<ProcessEntry> entries)
        {
            lock (bookLock)
            {
                if (lastSequenceNumber < LastSequenceNumber)
                    return;

                if (!bookSet)
                {
                    var entry = new ProcessBufferEntry()
                    {
                        FirstSequence = firstSequenceNumber,
                        LastSequence = lastSequenceNumber,
                        Entries = entries
                    };
                    processBuffer.Add(entry);
                    log.Write(LogVerbosity.Debug, $"{id} order book {Symbol} update before synced; buffering");
                }
                else if (sequencesAreConsecutive && firstSequenceNumber > LastSequenceNumber + 1)
                {
                    // Out of sync
                    log.Write(LogVerbosity.Warning, $"{id} order book {Symbol} out of sync, reconnecting");
                    subscription.Reconnect().Wait();
                }
                else
                {
                    foreach(var entry in entries)
                        ProcessUpdate(entry.Type, entry.Entry);
                    LastSequenceNumber = lastSequenceNumber;
                    CheckProcessBuffer();
                    log.Write(LogVerbosity.Debug, $"{id} order book {Symbol} update: {entries.Count} entries processed");
                }
            }
        }

        protected void CheckProcessBuffer()
        {
            foreach (var bufferEntry in processBuffer.OrderBy(b => b.FirstSequence).ToList())
            {
                if(bufferEntry.LastSequence < LastSequenceNumber)
                {
                    processBuffer.Remove(bufferEntry);
                    continue;
                }

                if (bufferEntry.FirstSequence > LastSequenceNumber + 1)
                    break;

                foreach(var entry in bufferEntry.Entries)
                    ProcessUpdate(entry.Type, entry.Entry);
                processBuffer.Remove(bufferEntry);
                LastSequenceNumber = bufferEntry.LastSequence;
            }
        }

        protected virtual void ProcessUpdate(OrderBookEntryType type, ISymbolOrderBookEntry entry)
        {
            var listToChange = type == OrderBookEntryType.Ask ? asks : bids;
            if (entry.Quantity == 0)
            {
                var bookEntry = listToChange.SingleOrDefault(i => i.Price == entry.Price);
                if (bookEntry != null)
                {
                    listToChange.Remove(bookEntry);
                    if (type == OrderBookEntryType.Ask) AskCount--;
                    else BidCount--;
                }
            }
            else
            {
                var bookEntry = listToChange.SingleOrDefault(i => i.Price == entry.Price);
                if (bookEntry == null)
                {
                    listToChange.Add(new OrderBookEntry(entry.Price, entry.Quantity));
                    if (type == OrderBookEntryType.Ask) AskCount++;
                    else BidCount++;
                }
                else
                    bookEntry.Quantity = entry.Quantity;
            }
        }

        public abstract void Dispose();
    }
}
