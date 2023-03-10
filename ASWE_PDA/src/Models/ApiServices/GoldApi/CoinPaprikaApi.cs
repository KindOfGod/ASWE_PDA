﻿using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ASWE_PDA.Models.ApiServices.GoldApi;

public class CoinPaprikaApi : ApiBase
{
    #region Fields

    private static readonly CoinPaprikaApi? _instance = null;

    #endregion

    #region Constructors

    private CoinPaprikaApi()
    {
        
    }

    #endregion

    #region Public Methods

    public async Task<Tuple<double, double>?> GetBitcoinEthereumPriceDollar()
    {
        try
        {
            var response = await MakeHttpRequest(
                "https://api.coinpaprika.com/v1/tickers"
            );

            if (response == null)
                return null;
            
            var bJsonArray = JArray.Parse(response);
            var bJObject = (JObject)bJsonArray?[0]?["quotes"]?["USD"]!;
        
            var bitcoinPrice = (double)(bJObject["price"] ?? 0);
            
            var eJsonArray = JArray.Parse(response);
            var eJObject = (JObject)eJsonArray?[1]?["quotes"]?["USD"]!;
        
            var ethereumPrice = (double)(eJObject["price"] ?? 0);
            
            return new Tuple<double, double>(bitcoinPrice, ethereumPrice);
        }
        catch
        {
            return null;
        }
    }

    public static CoinPaprikaApi GetInstance()
    {
        return _instance ?? new CoinPaprikaApi();
    }

    #endregion
}