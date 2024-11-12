using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using SimpleGraph.ViewModels;
using System;
using System.Net.WebSockets;
using System.Threading;

namespace SimpleGraph.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public void ConnectButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (connectButton.IsChecked == true)
        {
            tickerTextBox.IsEnabled = false;
            tickNumberTextBox.IsEnabled = false;
            //var tickerName = tickerTextBox.Text;
            //int tickNumber;
            //int.TryParse(tickNumberComboBox.SelectedItem as string, out tickNumber);
            connectButton.Content = "Disconnect";
            //Console.WriteLine(tickerName);
        }
        else
        {
            tickerTextBox.IsEnabled = true;
            tickNumberTextBox.IsEnabled = true;
            connectButton.Content = "Connect";
        }
    }
}
