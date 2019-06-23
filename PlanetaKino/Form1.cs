using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Net;
using HtmlAgilityPack;
using System.IO;
using Newtonsoft.Json;
using System.Drawing.Imaging;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace PlanetaKino
{
    public partial class Form1 : Form
    {
        WebClient client = new WebClient();
        String chosen_movie = null;
        String name_chosen = null;
        enum Months { Jan = 1, Feb, Mar, Apr, May, Jun, Jul, Aug, Sep, Oct, Nov, Dec };


        public Form1()
        {
            InitializeComponent();

            botClient = new TelegramBotClient("748401542:AAERQvTc1oWF78AK8crgAj52wRIC3L1n8oQ");
            botClient.OnMessage += Bot_OnMessage;
            botClient.StartReceiving();
           

            tabControl1.SelectedTab = tabPage1;
            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;

            string htmlCode = new WebClient().DownloadString("https://planetakino.ua/");


            HtmlAgilityPack.HtmlDocument htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.Load(new WebClient().OpenRead("https://planetakino.ua/"), Encoding.UTF8);
            //htmlDoc.LoadHtml(htmlCode);

            HtmlNodeCollection s = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'content__section')]");
            HtmlNodeCollection movie_blocks = s[0].SelectNodes("./div[contains(@class, 'movie-block')]");

            int y = tabControl1.Height / 2 - movie_blocks.Count * 30 / 2;
            foreach (HtmlNode movie in movie_blocks)
            {
                Button btn = new Button();
                btn.Width = 200;
                btn.Tag = movie.SelectSingleNode("./div[@class='movie-block__info']").SelectSingleNode("./a[@class='movie-block__text movie-block__text_title']").Attributes["href"].Value;
                btn.Text = movie.SelectSingleNode("./div[@class='movie-block__info']").SelectSingleNode("./a[@class='movie-block__text movie-block__text_title']").InnerText;
                tabPage1.Controls.Add(btn);
                btn.Location = new Point(btn.Parent.Width / 2 - btn.Width / 2, y);
                btn.Click += new System.EventHandler(this.btn_click);
                y += 30;
            }
        }
        public void btn_click(Object sender, EventArgs e)
        {
            list = new List<TreeNode>();
            var btn = sender as Button;
            chosen_movie = btn.Tag.ToString();
            name_chosen = btn.Text;
            label3.Text = btn.Text;
            GetSessions();
            tabControl1.SelectedTab = tabPage2;
            treeView1.ExpandAll();
        }
        public void after_select(Object sender, TreeViewEventArgs e)
        {
            if (e.Node.Nodes.Count == 0)
            {
                if (e.Node.Checked && !list.Contains(e.Node)) list.Add(e.Node);
                else list.Remove(e.Node);
            }
            TrackNodes(e.Node.Nodes);
        }

        List<TreeNode> list = new List<TreeNode>();
        public void TrackNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = node.Parent.Checked;
                if (node.Checked && node.Nodes.Count == 0 && !list.Contains(node))
                {
                    list.Add(node);
                }
                if (!node.Checked)
                {
                    list.Remove(node);
                }
                TrackNodes(node.Nodes);
            }
        }
        public void removeNodes(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node != null)
                {
                    removeNodes(node.Nodes);
                    list_remove.Add(node);
                }
            }
            foreach (TreeNode tn in list_remove)
            {
                nodes.Remove(tn);
            }
        }
        List<TreeNode> list_remove = new List<TreeNode>();
        private void GetSessions()
        {
            tabPage2.Controls.Remove(label2);
            removeNodes(treeView1.Nodes);
            string[] arr = chosen_movie.Split('/');
            string movie = arr[arr.Length - 2];
            var json = new WebClient().DownloadString($"https://planetakino.ua/movie-details/{movie}/");
            JObject o = JObject.Parse(json);
            String dateStart = (string)o["data"]["dateStart"];
            String dateEnd = (string)o["data"]["dateEnd"];
            String movieId = (string)o["data"]["movieID"];
            var json2 = new WebClient().DownloadString($"https://planetakino.ua/showtimes/json/?dateStart={dateStart}&dateEnd={dateEnd}&movieId={movieId}");
            JObject o2 = JObject.Parse(json2);



            JArray ja = (JArray)o2["showTimes"];


            JArray sorted = new JArray(ja.OrderBy(obj => Int32.Parse(obj["timeBegin"].ToString().Substring(5, 2))));
            JArray months = JArray.FromObject(sorted.Select(obj => Int32.Parse(obj["timeBegin"].ToString().Substring(5, 2))).Distinct());

            TreeView tv = treeView1;
            tv.CheckBoxes = true;
            tv.AfterCheck += new TreeViewEventHandler(this.after_select);
            for (int i = 0; i < months.Count; i++)
            {
                tv.Nodes.Add(Enum.GetName(typeof(Months), Int32.Parse(months[i].ToString())));

                JArray month_arr = JArray.FromObject(sorted.Where(obj => Int32.Parse(obj["timeBegin"].ToString().Substring(5, 2)) == Int32.Parse(months[i].ToString())).ToArray());
                JArray days = JArray.FromObject(month_arr.Select(obj => Int32.Parse(obj["timeBegin"].ToString().Substring(8, 2))).Distinct());

                for (int j = 0; j < days.Count; j++)
                {
                    tv.Nodes[i].Nodes.Add(days[j].ToString());

                    JArray day_arr = JArray.FromObject(month_arr.Where(obj => Int32.Parse(obj["timeBegin"].ToString().Substring(8, 2)) == Int32.Parse(days[j].ToString())).ToArray());
                    JArray halls = JArray.FromObject(day_arr.Select(obj => obj["technologyId"].ToString()).Distinct());

                    for (int k = 0; k < halls.Count; k++)
                    {
                        tv.Nodes[i].Nodes[j].Nodes.Add(halls[k].ToString());

                        JArray hall_arr = JArray.FromObject(day_arr.Where(obj => obj["technologyId"].ToString() == halls[k].ToString()).ToArray());


                        for (int l = 0; l < hall_arr.Count; l++)
                        {
                            TreeNode tn = new TreeNode();
                            Session s = new Session();
                            s.id = hall_arr[l]["id"].ToString();
                            s.name = name_chosen;
                            s.hall_id = Int32.Parse(hall_arr[l]["hallId"].ToString());
                            s.technology = hall_arr[l]["technologyId"].ToString();
                            tn.Tag = s;
                            tn.Text = hall_arr[l]["timeBegin"].ToString();
                            tv.Nodes[i].Nodes[j].Nodes[k].Nodes.Add(tn);
                        }
                    }
                }
            }
        }

        private void cancelAsyncButton_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.WorkerSupportsCancellation == true)
            {
                backgroundWorker1.CancelAsync();
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            for (int i = 0; i > -1; i++)
            {
                if (worker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
                //monitor();

                foreach (TreeNode item in list)
                {
                    Session s = (Session)item.Tag;
                    var json = client.DownloadString($"https://pay.planetakino.ua/api/v1/cart/halls?showtimeId={s.id}");
                    JObject o = JObject.Parse(json);
                    int ticketsPurchased = (int)o["data"]["hallsSheme"][0]["ticketsPurchased"];
                    int ticketsLeftForPurchasing = (int)o["data"]["hallsSheme"][0]["ticketsLeftForPurchasing"];
                    int ticketsLeftForBooking = (int)o["data"]["hallsSheme"][0]["ticketsLeftForBooking"];
                    //label1.Text = $"Tickets Purchased: { ticketsPurchased }\n Tickets Left: {ticketsLeftForPurchasing}";
                    JArray arr = (JArray)o["data"]["hallsSheme"][0]["emptySeats"];
                    if (arr == null)
                    {
                        arr = (JArray)o["data"]["hallsSheme"][0]["busySeats"];
                    }
                    if (s.time == "")
                    {
                        s.ticketsPurchased = ticketsPurchased;
                        s.ticketsLeftForPurchasing = ticketsLeftForPurchasing;
                        s.time = item.Text;
                        s.busy = arr.ToObject<List<String>>();
                    }
                    else
                    {
                        if (s.ticketsLeftForPurchasing != ticketsLeftForPurchasing)
                        {
                            String info = "";
                            if (s.ticketsLeftForPurchasing > ticketsLeftForPurchasing)
                            {
                                info = $"- {s.ticketsLeftForPurchasing - ticketsLeftForPurchasing} tickets | ";
                            }
                            else
                            {
                                info = $"+ {ticketsLeftForPurchasing - s.ticketsLeftForPurchasing} tickets | ";
                            }
                            Record rec = new Record(DateTime.Now.ToString("h:mm:ss tt") + ": " + info + s.name + " - " + s.technology + " - " + s.time + $" ({ticketsLeftForPurchasing} : {s.ticketsLeftForPurchasing})");
                            rec.hall_id = s.hall_id;
                            rec.busy1 = s.busy;
                            rec.busy2 = arr.ToObject<List<String>>();
                            if ((checkBox1.Checked == true && s.ticketsLeftForPurchasing < ticketsLeftForPurchasing) || (checkBox2.Checked == true && s.ticketsLeftForPurchasing > ticketsLeftForPurchasing))
                            { 
                                addRecord(rec);
                            }
                            //listBox1.Items.Insert(0, rec);

                            if (textBox1.Text != "" && ((checkBox1.Checked == true && s.ticketsLeftForPurchasing < ticketsLeftForPurchasing) || (checkBox2.Checked == true && s.ticketsLeftForPurchasing > ticketsLeftForPurchasing) ))
                            {
                                botClient.SendTextMessageAsync(
                                    chatId: textBox1.Text,
                                    text: info + s.name + " - " + s.technology + " - " + s.time + $" ({ticketsLeftForPurchasing} : {s.ticketsLeftForPurchasing})",
                                    parseMode: ParseMode.Markdown
                                //disableNotification: true,
                                );
                            }
                            s.ticketsPurchased = ticketsPurchased;
                            s.ticketsLeftForPurchasing = ticketsLeftForPurchasing;

                        }
                    }
                    //System.Threading.Thread.Sleep(500);
                }

                System.Threading.Thread.Sleep(10000);
            }
        }
        private delegate void addRecordCallback(Record rec);

        private void addRecord(Record rec)
        {
            if (listBox1.InvokeRequired)
            {
                addRecordCallback d = new addRecordCallback(addRecord);
                this.Invoke(d, new object[] { rec });
            }
            else
            {
                listBox1.Items.Insert(0, rec);
            }
        }
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                //textBox1.Text = "Canceled!\r\n" + textBox1.Text;
                listBox1.Items.Insert(0, new Record("Canceled!"));
            }
            else if (e.Error != null)
            {
                //textBox1.Text = "Error: " + e.Error.Message + "\r\n" + textBox1.Text;

            }
            else
            {
                //textBox1.Text = "Done!\r\n" + textBox1.Text;
            }
        }

        delegate void SetTextCallback();
        public void monitor()
        {
            if (this.listBox1.InvokeRequired)
            {
                //SetTextCallback d = new SetTextCallback(monitor);
                //this.Invoke(d, new object[] { });

            }
            else
            {
            }
        }
        public class Session
        {
            public Session() { }
            public Session(int ticketsPurchased, int ticketsLeftForPurchasing, string time, string id)
            {
                this.ticketsPurchased = ticketsPurchased;
                this.ticketsLeftForPurchasing = ticketsLeftForPurchasing;
                this.time = time;
                this.id = id;
            }
            public int ticketsPurchased { get; set; } = 0;
            public int ticketsLeftForPurchasing { get; set; } = 0;
            public string time { get; set; } = "";
            public string id = "";
            public string name = "";
            public string technology = "";
            public int hall_id = 0;
            public List<String> busy = null;
        }
        class Record
        {
            public Record() { }
            public Record(String text)
            {
                this.Text = text;
            }
            public List<String> busy1 = new List<string>();
            public List<String> busy2 = new List<string>();
            public int hall_id { get; set; } = 0;
            public String Text = null;
            public override string ToString()
            {
                return this.Text;
            }
        }
        //List<Session> sessions = new List<Session>();
        private void button2_Click(object sender, EventArgs e)
        {
            if (backgroundWorker1.IsBusy != true)
            {
                //this.textBox1.Text = "Started...\r\n" + this.textBox1.Text;
                listBox1.Items.Insert(0, new Record("Started..."));
                backgroundWorker1.RunWorkerAsync();
                tabControl1.SelectedTab = tabPage3;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {


        }
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

            tabControl1.SelectedTab = tabPage4;
            Record rec = listBox1.SelectedItem as Record;
            if (rec.hall_id != 0)
            {
                StreamReader sr = new StreamReader("halls.json");
                JsonTextReader reader = new JsonTextReader(sr);
                JObject halls = JObject.Load(reader);

                JArray arr = (JArray)halls[rec.hall_id.ToString()];
                int x_offset = Int32.Parse(arr[1]["x"].ToString());
                int y_offset = Int32.Parse(arr[1]["y"].ToString());
                System.Drawing.Pen myPen = new System.Drawing.Pen(System.Drawing.Color.Gray);

                System.Drawing.Graphics formGraphics;
                formGraphics = tabPage4.CreateGraphics();

                foreach (JToken point in arr)
                {
                    if (rec.busy1.Contains(point["id"].ToString()))
                    {
                        myPen.Color = System.Drawing.Color.Red;
                    }
                    else
                    {
                        myPen.Color = System.Drawing.Color.Gray;
                    }
                    formGraphics.DrawRectangle(myPen, new Rectangle(Int32.Parse(point["x"].ToString()) / 2 - x_offset / 2, Int32.Parse(point["y"].ToString()) / 2 - y_offset / 2, Int32.Parse(point["width"].ToString()) / 2, Int32.Parse(point["height"].ToString()) / 2));
                }
                y_offset -= 440;
                foreach (JToken point in arr)
                {
                    if (rec.busy2.Contains(point["id"].ToString()))
                    {
                        myPen.Color = System.Drawing.Color.Red;
                    }
                    else
                    {
                        myPen.Color = System.Drawing.Color.Gray;
                    }
                    formGraphics.DrawRectangle(myPen, new Rectangle(Int32.Parse(point["x"].ToString()) / 2 - x_offset / 2, Int32.Parse(point["y"].ToString()) / 2 - y_offset / 2, Int32.Parse(point["width"].ToString()) / 2, Int32.Parse(point["height"].ToString()) / 2));
                }
                myPen.Dispose();
                formGraphics.Dispose();
                System.Threading.Thread.Sleep(1000);

                //Rectangle bounds = tabPage4.Bounds;
                //using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                //{
                //    tabPage4.DrawToBitmap(bitmap, new Rectangle(0, 0, bounds.Width, bounds.Height));
                //    using (MemoryStream ms = new MemoryStream())
                //    {
                //        var stream = new MemoryStream();
                //        bitmap.Save(stream, ImageFormat.Jpeg);
                //        stream.Position = 0;

                //        botClient.SendPhotoAsync(
                //            chatId: textBox1.Text,
                //            //photo: "https://github.com/TelegramBots/book/raw/master/src/docs/photo-ara.jpg",
                //            photo: stream,
                //            caption: "Test"
                //        );
                //    }
                //}
            }
        }

        TelegramBotClient botClient;
        private void button3_Click_1(object sender, EventArgs e)
        {
        }
        delegate void SetChatIdCallBack(string text);
        public void SetChatId(string text)
        {
            if (this.label1.InvokeRequired)
            {
                SetChatIdCallBack d = new SetChatIdCallBack(SetChatId);
                this.Invoke(d, new object[] { text });

            }
            else
            {
                textBox1.Text = text;
                //button3.Enabled = true;
            }
        }
        public async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                SetChatId(e.Message.Chat.Id.ToString());
            }
        }

        //private void button4_Click(object sender, EventArgs e)
        //{
        //    botClient = new TelegramBotClient("748401542:AAERQvTc1oWF78AK8crgAj52wRIC3L1n8oQ");
        //    button4.Text = "Running";
        //    button4.Enabled = false;
        //    botClient.OnMessage += Bot_OnMessage;
        //    botClient.StartReceiving();

        //    botClient.SendTextMessageAsync(
        //        chatId: textBox1.Text,
        //        text: "test"
        //    );
        //}
        
    }
}
