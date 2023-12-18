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
using System.Collections.ObjectModel;
using Avalonia.Controls.Documents;

namespace MQTTAvalonia
{
    public partial class MainWindow : Window
    {
        #region Properties
        public string? BrokerUri { get; set; }
        public MqttClient? Client { get; set; }

        public EventHandler GoBackRequested;

        public string? m_TopicName { get; set; }
        public string? m_ReceivedMessage { get; set; }
        public bool m_IsConnected { get; set; } = false;

        public List<string> m_AvailableTopics { get; set; } = new List<string>();
        public List<string> m_SelectedTopics { get; set; } = new List<string>();
        public ObservableCollection<string> m_SubscribedTopics { get; set; } = new ObservableCollection<string>();


        public string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MQTT_Broker");

        public string DB_path = "MQTT_DB.db";

        public string m_connectionStatus;

        #endregion 

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }
        #endregion

        #region Methods

        public void setBrokerNameString(string brokerName)
        {
            tb_BrokerName.Text = brokerName;
        }

        public bool ConnectToBroker(string brokerURL)
        {
            BrokerUri = Convert.ToString(brokerURL?.Trim());
            if (string.IsNullOrEmpty(BrokerUri))
            {
                m_connectionStatus = "No Broker IP provided";
                connectionStatusCircle.Fill = Brushes.Red;
                return false;
            }

            try
            {
                Client = new MqttClient(BrokerUri);
                Client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                Client.MqttMsgPublished += Client_MqttMsgPublished;
            }
            catch (Exception ex)
            {
                m_connectionStatus = "Connection failed";
                connectionStatusCircle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
                return false;
            }

            string clientId = Guid.NewGuid().ToString();

            try
            {
                Client.Connect(clientId);
                m_IsConnected = Client.IsConnected;

                connectionStatusCircle.Fill = Brushes.Green;
            }
            catch (Exception ex)
            {
                m_connectionStatus = "Connection failed";
                connectionStatusCircle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
                return false;
            }
            return true;
        }

        private void Client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            string status;
            if (e.IsPublished)
            {
                status = "Publish succesful";
            }
            else
            {
                status = "Error: Publishing was not succesful";
            }
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                tb_qosStatus.Text = status;
            });
        }

        private void DisconnectClicked(object? sender, RoutedEventArgs e)
        {
            if (Client != null && Client.IsConnected)
            {
                Client.Disconnect();
                connectionStatusCircle.Fill = Brushes.Red;

                m_IsConnected = false;
                OnGoBackReuqetsed();
            }
            else
            {
                //ConnectionStatusTextBox.Text = "Client is either null or not connected.";
                // Handle the case where the client is null or not connected
                // This could involve displaying a message or logging an error
                Console.WriteLine("Client is either null or not connected.");
            }
        }

        public void OnGoBackReuqetsed()
        {
            GoBackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PublishMessage(object sender, RoutedEventArgs e)
        {
            if (lb_Subscriptions.SelectedIndex != -1)
            {
                string topic = m_SubscribedTopics[lb_Subscriptions.SelectedIndex];
                string message = tb_Message.Text.Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }
                byte qosLevel = getQosLevel(cb_QosPublish.SelectedIndex);
                Client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message), qosLevel, false);
            }
        }

        private byte getQosLevel(int index)
        {
            switch (index)
            {
                default: return MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE; break;
                case 1: return MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE; break;
                case 2: return MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE; break;
            }

        }
        private void DeleteReceivedMessages(object sender, RoutedEventArgs e)
        {
            tb_ReceivedMessage.Text = "";
        }

        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string receivedMessage = System.Text.Encoding.UTF8.GetString(e.Message);

            //string? subscribedTopicMatch = null;
            //subscribedTopicMatch = m_SubscribedTopics.FirstOrDefault(t => t.Equals(e.Topic));

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                //if (subscribedTopicMatch != null)
                UpdateReceivedMessages($"{e.Topic}:\t {receivedMessage}");
            });
        }

        private void UpdateReceivedMessages(string message)
        {
            tb_ReceivedMessage.Text += $"{message}\n\n";
        }

        private void ShowLastMessage(object sender, RoutedEventArgs e)
        {
            string
                lastTopicID = GetLastTopicIDFromDatabase();
            string
                lastTopic = GetTopic(lastTopicID);
            string
                lastMessage =
                    GetMessage(lastTopic);
            tb_Message.Text = lastMessage;
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

        private void tb_EnterTopicName_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(tb_EnterTopicName.Text?.Trim()))
                btn_Subscribe.IsEnabled = true;
            else
                btn_Subscribe.IsEnabled = false;

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
            AddToSubscribedList(tb_EnterTopicName.Text);
        }
        private void UnsubscribeFromTopic_Clicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (lb_Subscriptions.SelectedIndex != -1)
            {
                Client.Unsubscribe(new[] { m_SubscribedTopics[lb_Subscriptions.SelectedIndex] });
                m_SubscribedTopics.RemoveAt(lb_Subscriptions.SelectedIndex);

                lb_Subscriptions.ItemsSource = null;
                if (m_SubscribedTopics.Count > 0)
                {
                    lb_Subscriptions.ItemsSource = m_SubscribedTopics;
                }
                lb_Subscriptions.InvalidateVisual();
            }
        }

        #endregion
        private void AddToSubscribedList(string topic)
        {

            if (!m_SubscribedTopics.Contains(topic))
            {
                byte qosLevel = getQosLevel(cb_QosSubscribe.SelectedIndex);
                m_SubscribedTopics.Add(topic);
                lb_Subscriptions.ItemsSource = null;
                lb_Subscriptions.ItemsSource = m_SubscribedTopics;
                Client.Subscribe(new[] { topic }, new byte[] { qosLevel });
            }

        }
    }
}
