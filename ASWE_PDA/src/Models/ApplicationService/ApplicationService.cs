﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ASWE_PDA.Models.ApiServices.BoredApi;
using ASWE_PDA.Models.ApiServices.CatFactApi;
using ASWE_PDA.Models.ApiServices.ChuckNorrisApi;
using ASWE_PDA.Models.ApiServices.CoinPaprikaApi;
using ASWE_PDA.Models.ApiServices.ErgastApi;
using ASWE_PDA.Models.ApiServices.ExchangeRateApi;
using ASWE_PDA.Models.ApiServices.GoldApi;
using ASWE_PDA.Models.ApiServices.GoogleCalendarApi;
using ASWE_PDA.Models.ApiServices.InteractivityApi;
using ASWE_PDA.Models.ApiServices.OpenLigaDB;
using ASWE_PDA.Models.ApiServices.WeatherStationApi;
using ASWE_PDA.Models.ApiServices.WitzApi;
using ASWE_PDA.Models.ApplicationService.DataModel;
using ASWE_PDA.ViewModels;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Pv;

namespace ASWE_PDA.Models.ApplicationService;

public static class ApplicationService
{
    #region Fields

    private static Cheetah cheetah;
    private static PvRecorder recorder;

    

    public static bool IsVoiceEnabled = false;
    
    public static ObservableCollection<ChatMessage> Messages = new();
    
    private static Timer? _timerFinance;
    private static Timer? _timerSports;
    private static Timer? _timerGoodMorning;
    
    private static readonly SpeechSynthesizer SpeechSynthesizer = new();
    private static readonly SpeechSynthesizer SpeechSynthesizerGer = new();

    private static readonly SpeechRecognitionEngine SpeechRecognitionEngine = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-GB"));
    
    public static MainWindowViewModel? _mainWindowViewModel = null;
    
    #endregion
    
    #region Constructors

    static ApplicationService()
    {
        Init();
    }

    #endregion
    
    #region Private Methods

    private static void ProcessCheetahFrame(short[] pcm)
{
    if (cheetah == null)
    {
        return;
    }

    CheetahTranscript transcriptObj = cheetah.Process(pcm);
    if (!string.IsNullOrEmpty(transcriptObj.Transcript))
    {
        // Handle the partial transcript
        Console.Write(transcriptObj.Transcript);
    }
    if (transcriptObj.IsEndpoint)
    {
        // Handle the final transcript
        CheetahTranscript finalTranscriptObj = cheetah.Flush();
        Console.WriteLine(finalTranscriptObj.Transcript);
    }
}
    private static async Task ProcessAudio()
    {
        try
        {
            StringBuilder transcriptBuilder = new StringBuilder();

            while (true)
            {
                short[] pcm = recorder.Read();
                CheetahTranscript transcriptObj = cheetah.Process(pcm);

                if (!String.IsNullOrEmpty(transcriptObj.Transcript))
                {
                    Console.Write(transcriptObj.Transcript);
                    transcriptBuilder.Append(transcriptObj.Transcript);
                }

                if (transcriptObj.IsEndpoint)
                {
                    CheetahTranscript finalTranscriptObj = cheetah.Flush();
                    Console.WriteLine(finalTranscriptObj.Transcript);
                    transcriptBuilder.Append(finalTranscriptObj.Transcript);

                    // Call the method to handle Cheetah's recognition results with the whole sentence
                    await HandleCheetahRecognitionAsync(transcriptBuilder.ToString().ToLower());

                    // Clear the transcript builder for the next sentence
                    transcriptBuilder.Clear();
                }
            }
        }
        catch (CheetahActivationLimitException)
        {
            Console.WriteLine($"AccessKey has reached its processing limit.");
        }
    }

    
    /// <summary>
    /// Initializes Application
    /// </summary>
    private static void Init()
    {
        string accessKey = "";

        cheetah = Cheetah.Create(accessKey);
        recorder = PvRecorder.Create(-1, cheetah.FrameLength);
        recorder.Start();

        Task.Run(ProcessAudio);

        //SpeechSynthesizer.SelectVoiceByHints(VoiceGender.Female);
        SpeechSynthesizer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.NotSet, 0, CultureInfo.GetCultureInfo("en-GB"));
        SpeechSynthesizer.Rate = 3;

        SpeechSynthesizerGer.SelectVoiceByHints(VoiceGender.Female, VoiceAge.NotSet, 0, CultureInfo.GetCultureInfo("de-DE"));
        SpeechSynthesizerGer.Rate = 3;

        //SpeechRecognitionEngine.LoadGrammar(new DictationGrammar());
        //SpeechRecognitionEngine.SetInputToDefaultAudioDevice();
        //SpeechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);

        //SpeechRecognitionEngine.SpeechRecognized += OnSpeechRecognizedAsync;
        
        // events start the next day
        _timerFinance = new Timer(OnFinanceTimerElapsed, null, DateTime.Today.Add(new TimeSpan(22 + 24, 0, 0)) - DateTime.Now, TimeSpan.FromDays(1));
        _timerSports = new Timer(OnSportsTimerElapsed, null, DateTime.Today.Add(new TimeSpan(21 + 24, 0, 0)) - DateTime.Now, TimeSpan.FromDays(7));
        _timerGoodMorning = new Timer(OnGoodMorningTimerElapsed, null, DateTime.Today.Add(new TimeSpan(6 + 24, 0, 0)) - DateTime.Now, TimeSpan.FromDays(1));

        AddBotMessage("Hey, how can I help you?");
    }


    private static async Task HandleCheetahRecognitionAsync(string recognizedText)
    {
        string useCase = "";

        // Set of predefined grammar choices
        HashSet<string> predefinedChoices = new HashSet<string>
        {
            "helix", "stop", "hello",
            "finance", 
            "entertain", "entertainment", "joke", "chuck norris", "cat fact", 
            "sport", "football", "bundesliga", "formula one",
            "good morning", "activity", "weather",
            "what are you"
        };

        // Iterate through the predefined choices and check if they are present in the input string
        foreach (string choice in predefinedChoices)
        {
            if (recognizedText.Contains(choice))
            {
                Console.WriteLine("Match found: " + choice);
                useCase = choice;
            }
        }

        // Handle activation and stopping cases
        if (recognizedText == "helix" || recognizedText == "stop")
        {
            _mainWindowViewModel?.OnSpeechButtonClick();
            if (recognizedText == "stop") IsVoiceEnabled = false;
            return;
        }

        if(!IsVoiceEnabled)
            return;

        if (predefinedChoices.Contains(useCase))
        {
            AddUserMessage(recognizedText);
            switch (useCase)
        {
            case "hello":
                _mainWindowViewModel?.OnSpeechButtonClick();
                //AddUserMessage("Hello!");
                AddBotMessage("Greetings, what can I do for you?");
                break;
            case "what are you":
                //AddUserMessage("What are you?");
                string prompt = "You are a SMART PDA with the name HELIX, developed by the HighPerformerGang and it is a pleasure for you to help me. Explain to me in 25 or less words what you are.";
                string gpt4Response = await GetGPT4ResponseAsync(prompt);
                AddBotMessage(gpt4Response);
                //AddBotMessage(gpt4Response);
                break;
            case "finance":
                //AddUserMessage("Finance?");
                AddBotMessage(await GetFinanceReportAsync());
                break;
            case "entertain":
            case "entertainment":
                //AddUserMessage("Entertainment?");
                //AddBotMessage(await GetEntertainmentAsync());
                AddBotMessage(await GetJokeAsync(), true);
                AddBotMessage(await GetChuckNorrisJokeAsync());
                AddBotMessage(await GetCatFactAsync());
                break;
            case "joke":
                //AddUserMessage("Joke?");
                AddBotMessage(await GetJokeAsync(), true);
                break;
            case "chuck norris":
                //AddUserMessage("Chuck Norris Joke?");
                AddBotMessage(await GetChuckNorrisJokeAsync());
                break;
            case "cat fact":
                //AddUserMessage("Cat fact?");
                AddBotMessage(await GetCatFactAsync());
                break;
            case "sport":
                //AddUserMessage("Sports?");
                AddBotMessage(await GetSportsAsync());
                ShowFootballTable();
                break;
            case "football":
                //AddUserMessage("Football?");
                AddBotMessage(await GetLeadingFootballTeamsAsync());
                break;
            case "bundesliga":
                //AddUserMessage("Bundesliga?");
                AddBotMessage("Here is the Bundesliga table");
                ShowFootballTable();
                break;
            case "formula one":
                //AddUserMessage("F1?");
                AddBotMessage("Here are the leading F1 drivers: " + await GetLeadingF1Async());
                break;
            case "good morning":
                //AddUserMessage("Good morning!");
                AddBotMessage(await GetGoodMorningAsync());
                break;
            case "activity":
                //AddUserMessage("Activity?");
                AddBotMessage(await GetActivityAsync());
                break;
            case "weather":
                //AddUserMessage("Weather?");
                AddBotMessage(await GetWeatherAsync());

                break;
        }
        }
        else
        {
            // Handle the "chat" case - unrecognized input, send it to the ChatGPT API
            AddUserMessage(recognizedText);
            string gptResponse = await GetGPT4ResponseAsync(recognizedText); // Assuming you have the GetGPT4ResponseAsync method implemented
            AddBotMessage(gptResponse);
        }
    }


    /// <summary>
    /// Adds a message from the bot to the main chat.
    /// </summary>
    private static void AddBotMessage(string message, bool isGerman = false)
    {
        Messages.Add(new ChatMessage()
        {
            MessageText = message,
            MessageAlignment = HorizontalAlignment.Left,
            //MessageBackground = new SolidColorBrush(Color.FromRgb(123, 120, 121)),
            IsBotIconVisible = true
        });

        if (!isGerman) SpeechSynthesizer.SpeakAsync(message);
        else SpeechSynthesizerGer.SpeakAsync(message);
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

    private static void OnFinanceTimerElapsed(object? state)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            AddBotMessage("Hey it's 21:00! Here is your daily financial update. ");
            AddBotMessage(await GetFinanceReportAsync());
        });
    }
    
    private static void OnSportsTimerElapsed(object? state)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            AddBotMessage("Hey it's Sunday 18:00! Here is your weekly sports update. ");
            AddBotMessage(await GetSportsAsync());
        });
    }
    
    private static void OnGoodMorningTimerElapsed(object? state)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            AddBotMessage("Hey it's 6:00! Here is your daily update. ");
            AddBotMessage(await GetGoodMorningAsync());
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

    private static async Task<string> GetGPT4ResponseAsync(string prompt)
    {
        return await GPTApi.GetInstance().GetGPT4ResponseAsync(prompt);
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
        return "Here is a Chuck Norris joke: \n" + await ChuckNorrisApi.GetInstance().GetJokeAsync();
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

    #region Use Case: Good Morning
    
    /// <summary>
    /// Returns good morning information results
    /// </summary>
    private static async Task<string> GetGoodMorningAsync()
    {
        var activityTask = GetActivityAsync();
        var weatherTask = GetWeatherAsync();
        var meetingsTask = GetMeetingsAsync();

        await Task.WhenAll(activityTask, weatherTask, meetingsTask);

        var activity = await activityTask;
        var weather = await weatherTask;
        var meetings = await meetingsTask;

        var result = $"{activity} \n\n {weather} \n\n {meetings}";

        return result;
    }
    
    /// <summary>
    /// Returns a random acitvity
    /// </summary>
    private static async Task<string> GetActivityAsync()
    {
        return "Here is an activity for today:  \n" + await BoredApi.GetInstance().GetActivityAsync();
    }
    
    /// <summary>
    /// Returns the weather
    /// </summary>
    private static async Task<string> GetWeatherAsync()
    {
        return "Your daily weather forecast: \n" + await WeatherStationApi.GetInstance().GetWeatherAsync();
    }
    
    /// <summary>
    /// Returns the calendar meetings
    /// </summary>
    private static async Task<string> GetMeetingsAsync()
    {
        return "Your meetings: \n" + await GoogleCalendarApi.GetInstance().GetMeetingsAsync();
    }

    #endregion
}