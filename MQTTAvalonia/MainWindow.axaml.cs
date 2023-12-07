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

namespace MQTTAvalonia
{
	public partial class MainWindow : Window
	{
		// Broker URI und Port
		static string brokerUri = "broker.hivemq.com";

		// MQTT-Client erstellen
		MqttClient client = new MqttClient(brokerUri);
		private string receivedMessage;

		#region Properties

		public string? BindingTest { get; set; }

		public MqttClient? Client { get; set; }
		public string? BrokerUri { get; set; }
		public bool UseAuth { get; set; }
		public string? AuthUser { get; set; }
		public string? AuthPass { get; set; }
		public int? ConnectionStatus { get; set; }
		public List<string> AvailableTopics { get; set; } = new List<string>();
		public List<string> SelectedTopics { get; set; } = new List<string>();
		public string? Topic { get; set; }
		public string? Message { get; set; }

		public string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"MQTT_Broker");

		public string DB_path = "MQTT_DB.db";

		#endregion // Properties

		#region Constructor

		public MainWindow()
		{
			InitializeComponent();
			CreateDBIfNotExists();
			BindingTest = "Test123";
		}

		#endregion

		private void SubscribeToTopic(object sender, RoutedEventArgs e)
		{
			string topic = SubscribeTopicTextBox.Text;

			// Abonnieren des angegebenen Topics
			client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

			// Möglicherweise hier auch das Empfangen von Nachrichten behandeln
			// client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
		}

		#region Methods

		private void Connect(object? sender, RoutedEventArgs e)
		{
			// Optional: Event-Handler für eingehende Nachrichten
			string brokerUrl = BrokerUrlTextBox.Text;
			client = new MqttClient(brokerUrl);
			client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

			// Verbindung zum Broker herstellen
			string clientId = Guid.NewGuid().ToString();
			client.Connect(clientId);
		}

		private void Disconnect(object? sender, RoutedEventArgs e)
		{
			client.Disconnect();
		}

		private void PublishMessage(object sender, RoutedEventArgs e)
		{
			string topic = TopicTextBox.Text;
			string message = MessageTextBox.Text;

			// Aktualisieren der empfangenen Nachrichten, indem die neue Nachricht hinzugefügt wird
			UpdateReceivedMessages($"Topic: {topic}, Nachricht: {message}");

			// Hier kannst du den Publish-Vorgang fortsetzen, wenn gewünscht
			// client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
			// client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
		}

		private void DeleteReceivedMessages(object sender, RoutedEventArgs e)
		{
			ReceivedMessagesTextBox.Text = "";
		}




		private void Submit(object sender, RoutedEventArgs e)
		{
			// Nachricht veröffentlichen
			string topic = "test/topic";
			string message = "Hello, MQTT!";
			client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));

			// Auf Nachrichten in einem bestimmten Thema hören
			client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
		}

		private void Receive(object sender, RoutedEventArgs e)
		{
			// Angekommene Nachricht anzeigen
			ReceivedMessagesTextBox.Text = receivedMessage;
		}

		private void DisconnectClicked(object? sender, RoutedEventArgs e)
		{
			// Verbindung trennen
			client.Disconnect();
		}

		// Aktualisierte Methode zum Behandeln von empfangenen Nachrichten für abonnierte Topics
		private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
		{
			string receivedMessage = System.Text.Encoding.UTF8.GetString(e.Message);

			Dispatcher.UIThread.InvokeAsync(() =>
			{
				// Überprüfen, ob die empfangene Nachricht zu einem abonnierten Topic gehört
				string subscribedTopic = SelectedTopics.FirstOrDefault(t => t.Equals(e.Topic));
				if (subscribedTopic != null)
				{
					// Anzeigen der empfangenen Nachricht für das abonnierte Topic
					UpdateReceivedMessages($"Topic: {e.Topic}, Nachricht: {receivedMessage}");
				}
			});
		}

		private void UpdateReceivedMessages(string message)
		{
			ReceivedMessagesTextBox.Text += $"{message}\n";
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

		public string get_Topic(string id)
		{
			//get Topic from id
			using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
			{
				string Select_Topic = "SELECT TNAME FROM STOPIC WHERE TID = " + id;

				SqliteCommand cmd = new SqliteCommand(Select_Topic, con);

				string Result = Convert.ToString(cmd.ExecuteScalar());

				return (Result);
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
	}
}