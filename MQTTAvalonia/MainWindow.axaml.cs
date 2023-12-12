using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Data.Sqlite;
using System.Xml.Schema;
using Avalonia.Media;
using Tmds.DBus.Protocol;

namespace MQTTAvalonia
{
    public partial class MainWindow : Window
    {
        // Deklariere die XAML-Elemente, um sie in der C#-Klasse zu verwenden
        List<string> m_topicStringList = new List<string>();
        List<string> m_subscriptionStringList = new List<string>();

        #region Properties
        // Broker URI und Port
        // broker.hivemq.com
        public string? BrokerUri { get; set; }
        public MqttClient? Client { get; set; }

        //public bool UseAuth { get; set; }
        //public string? AuthUser { get; set; }
        //public string? AuthPass { get; set; }
        public string? TopicName { get; set; }
        public string? ReceivedMessage { get; set; }
        public bool IsConnected { get; set; } = false;

        public List<string> AvailableTopics { get; set; } = new List<string>();
        public List<string> SelectedTopics { get; set; } = new List<string>();
        public List<string> SubscribedTopics { get; set; } = new List<string>();


        public string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MQTT_Broker");

        public string DB_path = "MQTT_DB.db";

        #endregion // Properties

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            DisconnectedDisableControlAccess();
            //topicListBox.ItemsSource = AvailableTopics;
            //CreateDBIfNotExists();
        }


        #endregion



        #region Methods

        private void ConnectClicked(object? sender, RoutedEventArgs e)
        {
            BrokerUri = Convert.ToString(tb_BrokerUrl.Text?.Trim());
            if (string.IsNullOrEmpty(BrokerUri))
            {
                connectionStatusTextBlock.Text = "No Broker IP provided";
                connectionStatusRectangle.Fill = Brushes.Red;
                return;
            }

            try
            {
                Client = new MqttClient(BrokerUri);
                Client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            }
            catch (Exception ex)
            {
                connectionStatusTextBlock.Text = "Connection failed";
                connectionStatusRectangle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
                return;
            }

            // Verbindung zum Broker herstellen
            string clientId = Guid.NewGuid().ToString();

            try
            {
                Client.Connect(clientId);
                connectionStatusTextBlock.Text = "Connected";
                IsConnected = Client.IsConnected;

                connectionStatusRectangle.Fill = Brushes.Green;
            }
            catch (Exception ex)
            {
                connectionStatusTextBlock.Text = "Connection failed";
                connectionStatusRectangle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
                return;
            }
            ConnectedEnableControlAccess();
        }

        private void DisconnectClicked(object? sender, RoutedEventArgs e)
        {
            if (Client != null && Client.IsConnected)
            {
                Client.Disconnect();
                connectionStatusTextBlock.Text = "Disconnected";
                connectionStatusRectangle.Fill = Brushes.Red;

                IsConnected = false;
                DisconnectedDisableControlAccess();
            }
            else
            {
                //ConnectionStatusTextBox.Text = "Client is either null or not connected.";
                // Handle the case where the client is null or not connected
                // This could involve displaying a message or logging an error
                Console.WriteLine("Client is either null or not connected.");
            }
        }


        private void PublishMessage(object sender, RoutedEventArgs e)
        {
            if (lb_Topics.SelectedIndex == -1)
            {
                tb_Message.Text = "Select a topic";
                return;
            }

            string topic = AvailableTopics[lb_Topics.SelectedIndex];
            string message = tb_Message.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {

                return;
            }
            //AddTopicToSubscriptionList(topic);
            // Aktualisieren der empfangenen Nachrichten, indem die neue Nachricht hinzugefügt wird
            //UpdateReceivedMessages($"Topic: {topic}, Nachricht: {message}"); // Passiert wenn subscribed von allein.

            Client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
        }

        private void DeleteReceivedMessages(object sender, RoutedEventArgs e)
        {
            tb_ReceivedMessages.Text = "";
        }




        private void AddTopicButtonClicked(object? sender, RoutedEventArgs e)
        {
            TopicName = tb_EnterTopicName.Text.Trim();

            if (!string.IsNullOrEmpty(TopicName) && IsConnected && AvailableTopics.FirstOrDefault(item => item == TopicName) == null)
            {
                //Client.Publish(TopicName, System.Text.Encoding.UTF8.GetBytes("Topic created"));
                //AddTopicToList(TopicName);
                AvailableTopics.Add(TopicName);
                lb_Topics.ItemsSource = null;
                if (AvailableTopics.Count > 0)
                    lb_Topics.ItemsSource = AvailableTopics;
                else lb_Topics.Items.Clear();

            }
        }

        // Aktualisierte Methode zum Behandeln von empfangenen Nachrichten für abonnierte Topics
        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string receivedMessage = System.Text.Encoding.UTF8.GetString(e.Message);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Überprüfen, ob die empfangene Nachricht zu einem abonnierten Topic gehört
                string subscribedTopic = SubscribedTopics.FirstOrDefault(t => t.Equals(e.Topic));
                if (subscribedTopic != null)
                {
                    // Anzeigen der empfangenen Nachricht für das abonnierte Topic
                    UpdateReceivedMessages($"{e.Topic}:\t {receivedMessage}");
                }
            });
        }

        private void UpdateReceivedMessages(string message)
        {
            tb_ReceivedMessages.Text += $"{message}\n\n";
        }

        private void ShowLastMessage(object sender, RoutedEventArgs e)
        {
            string
                lastTopicID = GetLastTopicIDFromDatabase(); // Annahme: Methode, um die ID des letzten Topics abzurufen
            string
                lastTopic = GetTopic(lastTopicID); // Verwendung der GetTopic-Methode, um den Namen des Topics abzurufen
            string
                lastMessage =
                    GetMessage(lastTopic); // Verwendung der GetMessage-Methode, um die letzte Nachricht für das Topic abzurufen

            // Setzen der letzten Nachricht in die MessageTextBox
            tb_Message.Text = lastMessage;
        }


        private void ConnectedEnableControlAccess()
        {
            btn_Disconnect.IsEnabled = true;
            btn_Connect.IsEnabled = false;
            btn_AddTopic.IsEnabled = !string.IsNullOrWhiteSpace(tb_EnterTopicName.Text);
            tb_EnterTopicName.IsEnabled = true;
            tb_BrokerUrl.IsEnabled = false;
            tb_Message.IsEnabled = true;
            btn_Publish.IsEnabled = !string.IsNullOrWhiteSpace(tb_Message.Text);

        }
        private void DisconnectedDisableControlAccess()
        {

            btn_Disconnect.IsEnabled = false;
            btn_Connect.IsEnabled = !string.IsNullOrWhiteSpace(tb_BrokerUrl.Text);
            tb_EnterTopicName.IsEnabled = false;
            btn_AddTopic.IsEnabled = false;
            tb_BrokerUrl.IsEnabled = true;
            tb_Message.IsEnabled = false;
            btn_Publish.IsEnabled = false;

            lb_Subscriptions.ItemsSource = null;
            lb_Subscriptions.Items.Clear();
            lb_Topics.ItemsSource = null;
            lb_Topics.Items.Clear();
            SubscribedTopics.Clear();
            AvailableTopics.Clear();
            tb_Message.Clear();
            tb_ReceivedMessages.Clear();
        }




        #endregion

        #region Sqllite

        public void CreateDBIfNotExists()
        {
            if (!Directory.Exists(appdata))
            {
                Directory.CreateDirectory(appdata);
            }

            if (!File.Exists(Path.Combine(appdata, DB_path)))
            {
                File.Create(Path.Combine(appdata, DB_path)).Close();
            }

            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(appdata, DB_path)))
            {
                connection.Open();

                var createT1 =
                    "CREATE TABLE IF NOT EXISTS STOPIC (TID INTEGER, TNAME TEXT, MID INTEGER, PRIMARY KEY(TID))";
                var createT2 = "CREATE TABLE IF NOT EXISTS SMSG (MID INTEGER, MTEXT TEXT, PRIMARY KEY(MID))";

                var cmd = new SqliteCommand(createT1, connection);
                cmd.ExecuteNonQuery();

                cmd = new SqliteCommand(createT2, connection);
                cmd.ExecuteNonQuery();
            }
        }

        private string GetLastTopicIDFromDatabase()
        {
            using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectLastTopicID = "SELECT MAX(TID) FROM STOPIC";

                using (SqliteCommand cmd = new SqliteCommand(selectLastTopicID, con))
                {
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }



        public string GetTopic(string id)
        {
            using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectTopic = "SELECT TNAME FROM STOPIC WHERE TID = " + id;

                using (SqliteCommand cmd = new SqliteCommand(selectTopic, con))
                {
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }


        private string GetMessage(string topic)
        {
            using (SqliteConnection connect = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectMsg = "SELECT MTEXT FROM SMSG WHERE MID = (SELECT MID FROM STOPIC WHERE TNAME = @Topic)";
                using (SqliteCommand cmd = new SqliteCommand(selectMsg, connect))
                {
                    cmd.Parameters.AddWithValue("@Topic", topic);
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }

            #endregion

        }


        #region Events


        private void BrokerUrl_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tb_BrokerUrl.Text) && IsConnected != true)
            {
                btn_Connect.IsEnabled = true;
            }
            else
            {
                btn_Connect.IsEnabled = false;
            }
        }

        private void tb_EnterTopicName_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tb_EnterTopicName.Text?.Trim()))
                btn_AddTopic.IsEnabled = true;
            else
                btn_AddTopic.IsEnabled = false;

        }

        private void tb_Message_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tb_Message.Text?.Trim()))
                btn_Publish.IsEnabled = true;
            else
                btn_Publish.IsEnabled = false;
        }


        private void SubscribeToTopic_Clicked(object sender, RoutedEventArgs e)
        {
            if (lb_Topics.SelectedIndex != -1)
            {
                string topic = AvailableTopics[lb_Topics.SelectedIndex];

                // Abonnieren des angegebenen Topics
                Client.Subscribe(new[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                SubscribedTopics.Add(topic);

                lb_Subscriptions.ItemsSource = null;
                lb_Subscriptions.ItemsSource = SubscribedTopics;
            }

        }
        private void UnsubscribeFromTopic_Clicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (lb_Subscriptions.SelectedIndex != -1)
            {
                Client.Unsubscribe(new[] { SubscribedTopics[lb_Subscriptions.SelectedIndex] });
                SubscribedTopics.RemoveAt(lb_Subscriptions.SelectedIndex);

                lb_Subscriptions.ItemsSource = null;
                if (SubscribedTopics.Count > 0)
                    lb_Subscriptions.ItemsSource = SubscribedTopics;
                else lb_Subscriptions.Items.Clear();
            }
        }

        #endregion
    }
}
