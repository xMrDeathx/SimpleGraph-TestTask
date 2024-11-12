using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using System.Collections.Generic;
using LiveChartsCore.Defaults;
using System.Collections.ObjectModel;
using System;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive;
using System.Net.WebSockets;
using System.Threading;
using System.Text.Json;

namespace SimpleGraph.ViewModels;

public class MainViewModel : ViewModelBase
{
    private ClientWebSocket _wsClient = new();
    private readonly Uri _baseUri = new("wss://ws-api.binance.com:443/ws-api/v3");
    private byte[] _buffer = new byte[1056];
    private byte[] _recieveBuffer = new byte[1056];
    private double _recievedBidPrice;
    private double _recievedAskPrice;
    private readonly Random _random = new();
    private readonly List<DateTimePoint> _bidValues = [];
    private readonly List<DateTimePoint> _askValues = [];
    private readonly DateTimeAxis _customAxis;
    private string _tickerName = "";
    private double _tickNumber = 1;
    public ObservableCollection<ISeries> Series { get; set; }
    public object Sync { get; } = new object();
    public bool IsReading { get; set; } = true;
    public ReactiveCommand<Unit, Task> DrawGraphCommand { get; }
    public ReactiveCommand<Unit, Task> StopDrawingCommand { get; }
    public string TickerName
    {
        get => _tickerName;
        set => this.RaiseAndSetIfChanged(ref _tickerName, value);
    }
    public double TickNumber
    {
        get => _tickNumber;
        set => this.RaiseAndSetIfChanged(ref _tickNumber, value);
    }
    public MainViewModel()
    {
        Series = [
            new LineSeries<DateTimePoint>
            {
                Name = "Bid",
                Values = _bidValues,
                Fill = null
            },
            new LineSeries<DateTimePoint>
            {
                Name = "Ask",
                Values = _askValues,
                Fill = null
            }
         ];

        _customAxis = new DateTimeAxis(TimeSpan.FromSeconds(1), Formatter)
        {
            Name = "Time",
            NamePaint = new SolidColorPaint(SKColors.White),
            TextSize = 18,
            Padding = new Padding(5, 15, 5, 5),
            LabelsPaint = new SolidColorPaint(SKColors.White),
            CustomSeparators = GetSeparators(),
            AnimationsSpeed = TimeSpan.FromMilliseconds(0),
            SeparatorsPaint = new SolidColorPaint(SKColors.White)
        };

        XAxes = [_customAxis];

        DrawGraphCommand = ReactiveCommand.Create(DrawGraph);

        StopDrawingCommand = ReactiveCommand.Create(StopDrawing);
    }

    private async Task StopDrawing()
    {
        await Task.Delay(100);
        IsReading = false;
        await Disconnect();
    }

    private async Task DrawGraph()
    {
        await ClearGraph();

        await Connect();

        IsReading = true;

        while (_wsClient.State == WebSocketState.Open)
        {
            await Task.Delay(25);
            await RecieveData();

            lock (Sync)
            {
                _bidValues.Add(new DateTimePoint(DateTime.Now, _recievedBidPrice));
                _askValues.Add(new DateTimePoint(DateTime.Now, _recievedAskPrice));
                if (_bidValues.Count > _tickNumber && _askValues.Count > _tickNumber)
                {
                    _bidValues.RemoveAt(0);
                    _askValues.RemoveAt(0);
                }
                _customAxis.CustomSeparators = GetSeparators();
            }
        }
    }

    private async Task ClearGraph()
    {
        await Task.Delay(100);

        lock (Sync)
        {
            _bidValues.Clear();
            _askValues.Clear();
        }
    }
    private static double[] GetSeparators()
    {
        var now = DateTime.Now;

        return
        [
            now.AddSeconds(-50).Ticks,
            now.AddSeconds(-40).Ticks,
            now.AddSeconds(-30).Ticks,
            now.AddSeconds(-20).Ticks,
            now.AddSeconds(-10).Ticks,
            now.Ticks
        ];
    }

    private static string Formatter(DateTime date)
    {
        var secsAgo = (DateTime.Now - date).TotalSeconds;

        if (secsAgo < 1)
        {
            return "now";
        }
        else
        {
            return $"{secsAgo}s ago";
        }
    }
    public Axis[] XAxes { get; set; } 
    public Axis[] YAxes { get; set; } =
    {
        new Axis
        {
            Name = "Price",
            NamePaint = new SolidColorPaint(SKColors.White),
            TextSize = 18,
            Padding = new Padding(5, 15, 5, 5),
            LabelsPaint = new SolidColorPaint(SKColors.White),
        }
    };

    public LabelVisual Title { get; set; } = new LabelVisual
    {
        Text = "Tick graph",
        TextSize=25,
        Padding= new LiveChartsCore.Drawing.Padding(15),
        Paint = new SolidColorPaint(SKColors.White)
    };

    public SolidColorPaint LegendTextPaint { get; set; } = new SolidColorPaint
    {
        Color = SKColors.White,
    };
    private async Task Connect()
    {
        await _wsClient.ConnectAsync(_baseUri, CancellationToken.None);
    }

    private async Task Disconnect()
    {
        await _wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
    }

    private async Task RecieveData()
    {
        var requestData = "{\r\n  \"id\": \"057deb3a-2990-41d1-b58b-98ea0f09e1b4\",\r\n  \"method\": \"ticker.book\",\r\n  \"params\": {\r\n    \"symbol\": \""+_tickerName+"\"\r\n  }\r\n}";
        _buffer = Encoding.UTF8.GetBytes(requestData);
        await _wsClient.SendAsync(new ArraySegment<byte>(_buffer), WebSocketMessageType.Text, true, CancellationToken.None);

        var recieveResult = await _wsClient.ReceiveAsync(new ArraySegment<byte>(_recieveBuffer), CancellationToken.None);
        var recievedData = Encoding.UTF8.GetString(_recieveBuffer, 0, recieveResult.Count);
        string[] recievedParams = recievedData.Split(',');

        foreach ( var param in recievedParams )
        {
            IsReading = true;
            if (param.Contains("bidPrice"))
            {
                string price = param.Substring(12);
                string resultPrice = price.Remove(price.Length - 1);
                _recievedBidPrice = double.Parse(resultPrice, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (param.Contains("askPrice"))
            {
                string price = param.Substring(12);
                string resultPrice = price.Remove(price.Length - 1);
                _recievedAskPrice = double.Parse(resultPrice, System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
