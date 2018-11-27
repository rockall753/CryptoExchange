﻿using System;
using System.Collections.Generic;
using System.IO;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.RateLimiter;

namespace CryptoExchange.Net.Objects
{
    /// <summary>
    /// Options
    /// </summary>
    public class ExchangeOptions
    {

        /// <summary>
        /// The api credentials
        /// </summary>
        public ApiCredentials ApiCredentials { get; set; }

        /// <summary>
        /// The base address of the client
        /// </summary>
        public string BaseAddress { get; set; }

        /// <summary>
        /// Proxy to use
        /// </summary>
        public ApiProxy Proxy { get; set; }
        
        /// <summary>
        /// The log verbosity
        /// </summary>
        public LogVerbosity LogVerbosity { get; set; } = LogVerbosity.Info;

        /// <summary>
        /// The log writers
        /// </summary>
        public List<TextWriter> LogWriters { get; set; } = new List<TextWriter>() {new DebugTextWriter()};        
    }

    public class ClientOptions: ExchangeOptions
    {
        /// <summary>
        /// List of ratelimiters to use
        /// </summary>
        public List<IRateLimiter> RateLimiters { get; set; } = new List<IRateLimiter>();

        /// <summary>
        /// What to do when a call would exceed the rate limit
        /// </summary>
        public RateLimitingBehaviour RateLimitingBehaviour { get; set; } = RateLimitingBehaviour.Wait;

        /// <summary>
        /// The time the server has to respond to a request before timing out
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class SocketClientOptions: ExchangeOptions
    {
        /// <summary>
        /// Time to wait between reconnect attempts
        /// </summary>
        public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
