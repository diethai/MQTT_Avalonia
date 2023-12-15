using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace MQTTAvalonia
{
    public partial class HomeWindow : Window
    {
        private MainWindow m_mainWindow;
        public HomeWindow()
        {
            InitializeComponent();
        }

        private void ConnectButtonClicked(object sender, RoutedEventArgs e)
        {
            m_mainWindow = new MainWindow();
            if (!m_mainWindow.ConnectToBroker(tb_BrokerURL.Text))
            {
                tb_status.Text = m_mainWindow.m_connectionStatus;
                return;
            }
            m_mainWindow.GoBackRequested += OnGoBackRequested;
            m_mainWindow.setBrokerNameString(tb_BrokerURL.Text);
            this.Hide();
            m_mainWindow.Show();
        }

        private void OnGoBackRequested(object sender, EventArgs e)
        {
            m_mainWindow.Close();
            this.Show();
        }
    }
}
