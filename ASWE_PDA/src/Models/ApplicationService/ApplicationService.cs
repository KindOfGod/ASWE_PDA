using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using ASWE_PDA.Models.ApiServices.CatFactApi;
using ASWE_PDA.Models.ApiServices.ChuckNorrisApi;
using ASWE_PDA.Models.ApiServices.CoinPaprikaApi;
using ASWE_PDA.Models.ApiServices.ErgastApi;
using ASWE_PDA.Models.ApiServices.ExchangeRateApi;
using ASWE_PDA.Models.ApiServices.GoldApi;
using ASWE_PDA.Models.ApiServices.OpenLigaDB;
using ASWE_PDA.Models.ApiServices.WitzApi;
using ASWE_PDA.Models.ApplicationService.DataModel;
using ASWE_PDA.ViewModels;
using Avalonia.Layout;
using Avalonia.Media;

namespace ASWE_PDA.Models.ApplicationService;

public static class ApplicationService
{
    #region Fields

    public static bool IsVoiceEnabled = false;
    
    public static ObservableCollection<ChatMessage> Messages = new();
    
    private static readonly SpeechSynthesizer SpeechSynthesizer = new();
    private static readonly SpeechRecognitionEngine SpeechRecognitionEngine = new();

    public static MainWindowViewModel? _mainWindowViewModel = null;
    
    #endregion
    
    #region Constructors

    static ApplicationService()
    {
        Init();
    }

    #endregion
    
    #region Private Methods
    
    /// <summary>
    /// Initializes Application
    /// </summary>
    private static void Init()
    {
        SpeechSynthesizer.SelectVoiceByHints(VoiceGender.Female);
        SpeechSynthesizer.Rate = 3;

        var vocabulary = new Choices();
        vocabulary.Add(
            "helix", "stop", "hello",
            "finance", 
            "entertain", "entertainment", "joke", "chuck norris", "cat fact", 
            "sport", "football", "bundesliga", "formula one");

        var grammarBuilder = new GrammarBuilder();
        grammarBuilder.Append(vocabulary);

        var grammar = new Grammar(grammarBuilder);
        
        SpeechRecognitionEngine.LoadGrammar(grammar);
        SpeechRecognitionEngine.SetInputToDefaultAudioDevice();
        SpeechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);

        SpeechRecognitionEngine.SpeechRecognized += OnSpeechRecognizedAsync;
        
        AddBotMessage("Hey, how can I help you?");
    }

    /// <summary>
    /// Handles Speech Recognition
    /// </summary>
    private static async void OnSpeechRecognizedAsync(object? sender, SpeechRecognizedEventArgs e)
    {
        // toggle activation
        switch (e.Result.Text.ToLower())
        {
            case "helix":
                _mainWindowViewModel?.OnSpeechButtonClick();
                break;
            case "stop":
                _mainWindowViewModel?.OnSpeechButtonClick();
                IsVoiceEnabled = false;
                break;
        }

        if(!IsVoiceEnabled)
            return;
        
        switch (e.Result.Text.ToLower())
        {
            case "hello":
                _mainWindowViewModel?.OnSpeechButtonClick();
                AddUserMessage("Hello!");
                AddBotMessage("Greetings, what can I do for you?");
                break;
            case "finance":
                AddUserMessage("Finance?");
                AddBotMessage(await GetFinanceReportAsync());
                break;
            case "entertain":
            case "entertainment":
                AddUserMessage("Entertainment?");
                AddBotMessage(await GetEntertainmentAsync());
                break;
            case "joke":
                AddUserMessage("Joke?");
                AddBotMessage(await GetJokeAsync());
                break;
            case "chuck norris":
                AddUserMessage("Chuck Norris Joke?");
                AddBotMessage(await GetChuckNorrisJokeAsync());
                break;
            case "cat fact":
                AddUserMessage("Cat fact?");
                AddBotMessage(await GetCatFactAsync());
                break;
            case "sport":
                AddUserMessage("Sports?");
                AddBotMessage(await GetSportsAsync());
                ShowFootballTable();
                break;
            case "football":
                AddUserMessage("Football?");
                AddBotMessage(await GetLeadingFootballTeamsAsync());
                break;
            case "bundesliga":
                AddUserMessage("Bundesliga?");
                AddBotMessage("Here is the Bundesliga table");
                ShowFootballTable();
                break;
            case "formula one":
                AddUserMessage("F1?");
                AddBotMessage("Here are the leading F1 drivers: " + await GetLeadingF1Async());
                break;
        }
    }
    
    /// <summary>
    /// Adds a message from the bot to the main chat.
    /// </summary>
    private static void AddBotMessage(string message)
    {
        Messages.Add(new ChatMessage()
        {
            MessageText = message,
            MessageAlignment = HorizontalAlignment.Left,
            MessageBackground = new SolidColorBrush(Color.FromRgb(123, 120, 121)),
            IsBotIconVisible = true
        });
        
        SpeechSynthesizer.SpeakAsync(message);
    }
    
    /// <summary>
    /// Adds a message from the user to the main chat.
    /// </summary>
    private static void AddUserMessage(string message)
    {
        Messages.Add(new ChatMessage()
        {
            MessageText = message
        });
    }
    
    #endregion

    #region Use Case: Finance
    
    /// <summary>
    /// Returns financial results
    /// </summary>
    private static async Task<string> GetFinanceReportAsync()
    {
        var coinPaprika = CoinPaprikaApi.GetInstance();
        var goldApi = GoldApi.GetInstance();
        var exchangeRateApi = ExchangeRateApi.GetInstance();

        var bitcoinEthereumTask = coinPaprika.GetBitcoinEthereumPriceDollarAsync();
        var goldSilverTask = goldApi.GetGoldSliverPriceDollarAsync();
        var exchangeRateTask = exchangeRateApi.GetUSDtoEURAsync();

        await Task.WhenAll(bitcoinEthereumTask, goldSilverTask, exchangeRateTask);
        
        var bitcoinEthereum = await bitcoinEthereumTask;
        var goldSilver = await goldSilverTask;
        var exchangeRate = await exchangeRateTask;

        var bitcoin = (bitcoinEthereum?.Item1 * exchangeRate) ?? 0;
        var ethereum = (bitcoinEthereum?.Item2 * exchangeRate) ?? 0;
        var gold = (goldSilver?.Item1 * exchangeRate) ?? 0;
        var silver = (goldSilver?.Item2 * exchangeRate) ?? 0;
    
        return $"Here are the current exchange rates: \n\n Bitcoin: {Math.Round(bitcoin, 2)}€ \n Ethereum: {Math.Round(ethereum, 2)}€ \n Gold: {Math.Round(gold, 2)}€ \n Silver: {Math.Round(silver, 2)}€";
    }
    
    #endregion

    #region Use Case: Entertainment
    /// <summary>
    /// Returns Entertainment results
    /// </summary>
    private static async Task<string> GetEntertainmentAsync()
    {
        var jokeTask = GetJokeAsync();
        var cnJokeTask = GetChuckNorrisJokeAsync();
        var catFactTask = GetCatFactAsync();
        
        await Task.WhenAll(jokeTask, cnJokeTask, catFactTask);

        var joke = await jokeTask;
        var cnJoke = await cnJokeTask;
        var catFact = await catFactTask;

        var result = $"{joke} \n\n {cnJoke} \n\n {catFact}";

        return result;
    }
    
    /// <summary>
    /// Returns Joke
    /// </summary>
    private static async Task<string> GetJokeAsync()
    {
        return "Here is a joke: \n" + await WitzApi.GetInstance().GetJokeAsync();
    }
    
    /// <summary>
    /// Returns Chuck Norris Joke
    /// </summary>
    private static async Task<string> GetChuckNorrisJokeAsync()
    {
        return "Here is a Chuck Norris joke: \n" + await CheckNorrisApi.GetInstance().GetJokeAsync();
    }
    
    /// <summary>
    /// Returns Cat Fact
    /// </summary>
    private static async Task<string> GetCatFactAsync()
    {
        return "Here is a cat fact: \n" + await CatFactApi.GetInstance().GetCatFactAsync();
    }

    #endregion

    #region Use Case: Sports

    /// <summary>
    /// Returns Sport results
    /// </summary>
    private static async Task<string> GetSportsAsync()
    {
        var footballTask = GetLeadingFootballTeamsAsync();
        var f1Task = GetLeadingF1Async();

        await Task.WhenAll(footballTask, f1Task);

        var football = await footballTask;
        var f1 = await f1Task;

        var result = $"{football} \n\n {f1}";

        return result;
    }
    
    /// <summary>
    /// Return leading football teams
    /// </summary>
    private static async Task<string> GetLeadingFootballTeamsAsync()
    {
        return "Here are the leading teams: \n" + await OpenLigaDbApi.GetInstance().GetLeadingTeamsAsync();
    }
    
    /// <summary>
    /// Show leading football teams
    /// </summary>
    private static void ShowFootballTable()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.bundesliga.com/de/bundesliga/tabelle",
            UseShellExecute = true
        });
    }
    
    /// <summary>
    /// Show leading F1 drivers
    /// </summary>
    private static async Task<string>  GetLeadingF1Async()
    {
        return "Here are the leading drivers: \n" + await ErgastApi.GetInstance().GetLatestF1ResultsAsync();
    }

    #endregion
}