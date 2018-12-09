using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

using FelicaLib;

namespace SuicaApp
{
    /// <summary>
    /// 路線・駅コード
    /// 参考：https://github.com/MasanoriYONO/StationCode
    /// </summary>
    struct StationCode
    {
        //地区コード
        public byte AreaCode;
        //線区コード
        public byte LineCode;
        //駅順コード
        public byte StationSeqCode;
        //会社名
        public string Company;
        //線区名
        public string Line;
        //駅名
        public string Station;
    }

    /// <summary>
    /// Suicaから読み取るデータ
    /// </summary>
    struct ParseData
    {
        // 0: 機器種別
        public byte TerminalNum;
        // 1: 利用種別
        public byte UsageType;
        // 2: 決済種別
        public byte SettlementType;
        // 3: 入出場種別
        public byte EntranceType;
        // 4-5: 年月日[年 / 7ビット、月 / 4ビット、日 / 5ビット]
        public int Year;
        public int Month;
        public int Day;
        // 6-9: 入出場駅コード(鉄道)、停留所コード(バス)、物販情報(物販)
        public byte InLineCode;
        public byte InStationCode;
        public byte OutLineCode;
        public byte OutStationCode;
        // 10-11: 残額
        public UInt16 Balance;
        // 13-14 履歴連番
        public UInt16 HistoryNum;

        // 駅名
        public string InLineStr;
        public string InStationStr;
        public string OutLineStr;
        public string OutStationStr;
        // 利用額
        public int Charge;
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// StationCode.csvの内容を保持するリスト
        /// </summary>
        private readonly List<StationCode> stationCodeList = new List<StationCode>();

        /// <summary>
        /// Suicaから読み込んだデータを保持するリスト
        /// （利用額を計算するために保持）
        /// </summary>
        private List<ParseData> parseData = new List<ParseData>();

        // 音声再生用
        private readonly System.Media.SoundPlayer playerOK;
        private readonly System.Media.SoundPlayer playerNG;
        private readonly string SoundFile = "OK.wav";
        private readonly string SoundFileNG = "NG.wav";

        public MainWindow()
        {
            InitializeComponent();
            ReadCsv();
            playerOK = new System.Media.SoundPlayer(SoundFile);
            playerNG = new System.Media.SoundPlayer(SoundFileNG);
        }

        /// <summary>
        /// CSVファイルの内容をリストに保持する
        /// </summary>
        private void ReadCsv()
        {
            try
            {
                // csvファイルを開く
                using (var sr = new System.IO.StreamReader(@"./StationCode.csv"))
                {
                    // ストリームの末尾まで繰り返す
                    while (!sr.EndOfStream)
                    {
                        // ファイルから一行読み込む
                        var line = sr.ReadLine();

                        if (line != null)
                        {
                            // 読み込んだ一行をカンマ毎に分けて配列に格納する
                            var values = line.Split(',');

                            // 一行目を飛ばす
                            bool result = byte.TryParse(values[0], out _);
                            if (!result)
                            {
                                continue;
                            }

                            // リストに追加する
                            StationCode sc;
                            sc.AreaCode = Convert.ToByte(values[0].Trim(), 16);
                            sc.LineCode = Convert.ToByte(values[1].Trim(), 16);
                            sc.StationSeqCode = Convert.ToByte(values[2].Trim(), 16);
                            sc.Company = values[3];
                            sc.Line = values[4];
                            sc.Station = values[5];
                            stationCodeList.Add(sc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // ファイルを開くのに失敗したとき
                textBox.Text = ex.Message;
            }
        }

        /// <summary>
        /// Suicaの読み取りを開始する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            textBox.Clear();
            parseData.Clear();
            try
            {
                using (var f = new Felica())
                {
                    ParseSuica(f);
                    // 利用額の計算
                    CalcCharge();
                    playerOK.Play();

                    // 保存
                    WriteCsv();
                }
            }
            catch (Exception ex)
            {
                textBox.Text = ex.Message;
                playerNG.Play();
            }
        }

        /// <summary>
        /// Suicaの乗降履歴情報をパースする
        /// 参考:https://www.wdic.org/w/RAIL/%E3%82%B5%E3%82%A4%E3%83%90%E3%83%8D%E8%A6%8F%E6%A0%BC%20%28IC%E3%82%AB%E3%83%BC%E3%83%89%29#090Fx20xE4xB9x97xE9x99x8DxE5xB1xA5xE6xADxB4xE6x83x85xE5xA0xB1
        /// </summary>
        /// <param name="f"></param>
        private void ParseSuica(Felica f)
        {
            // システムコード: 0003 (Suicaなどの領域)
            f.Polling((int)SystemCode.Suica);

            for (int i = 0; ; i++)
            {
                ParseData ps;

                // サービスコード　乗降履歴情報(16バイト)
                byte[] data = f.ReadWithoutEncryption(0x090f, i);
                if (data == null) break;

                // 0: 機器種別
                ps.TerminalNum = data[0];

                // 1: 利用種別
                ps.UsageType = data[1];

                // 2: 決済種別
                ps.SettlementType = data[2];

                // 3: 入出場種別
                ps.EntranceType = data[3];

                // 4-5: 年月日[年 / 7ビット、月 / 4ビット、日 / 5ビット]
                ps.Year = (data[4] >> 1) + 2000;
                ps.Month = ((data[4] & 0x01) << 3) + (data[5] >> 5);
                ps.Day = data[5] & 0x1F;

                // 6-9: 入出場駅コード(鉄道)、停留所コード(バス)、物販情報(物販)
                ps.InLineCode = data[6];
                ps.InStationCode = data[7];
                ps.OutLineCode = data[8];
                ps.OutStationCode = data[9];

                // コードから駅名を探す
                ps.InLineStr = "";
                ps.InStationStr = "";
                ps.OutLineStr = "";
                ps.OutStationStr = "";
                foreach (var stationCode in stationCodeList)
                {
                    if (stationCode.LineCode == ps.InLineCode && stationCode.StationSeqCode == ps.InStationCode)
                    {
                        ps.InLineStr = stationCode.Line;
                        ps.InStationStr = stationCode.Station;
                        break;
                    }
                }
                foreach (var stationCode in stationCodeList)
                {
                    if (stationCode.LineCode == ps.OutLineCode && stationCode.StationSeqCode == ps.OutStationCode)
                    {
                        ps.OutLineStr = stationCode.Line;
                        ps.OutStationStr = stationCode.Station;
                        break;
                    }
                }

                // 10-11: 残額
                ps.Balance = (UInt16)((data[11] << 8) + data[10]);

                // 13-14 履歴連番
                ps.HistoryNum = (UInt16)((data[13] << 8) + data[14]);

                ps.Charge = 0;

                parseData.Add(ps);
            }
        }

        /// <summary>
        /// 利用額を計算する
        /// </summary>
        private void CalcCharge()
        {
            // 20件目のデータは計算できないので注意
            for (int i = 0; i < parseData.Count - 1; ++i)
            {
                ParseData tmpData = parseData[i];
                tmpData.Charge = parseData[i + 1].Balance - parseData[i].Balance;
                parseData[i] = tmpData;
            }
        }

        /// <summary>
        /// 読み取ったSuicaのデータをCSVファイルに書き込む
        /// </summary>
        private void WriteCsv()
        {
            for (int i = 0; i < parseData.Count; ++i)
            {
                textBox.Text += "乗降履歴情報 [" + i + "]  " +
                                parseData[i].Year.ToString() + "/" + parseData[i].Month.ToString() + "/" + parseData[i].Day.ToString() + "," +
                                parseData[i].InLineStr + "," + parseData[i].InStationStr + "," +
                                parseData[i].OutLineStr + "," + parseData[i].OutStationStr + ", 残高：" +
                                parseData[i].Balance.ToString() + ", 利用額:" +
                                parseData[i].Charge.ToString() + ", 履歴番号:" +
                                parseData[i].HistoryNum.ToString() + "\n";

            }

            try
            {
                string dirName = "./output";
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }

                string now = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = dirName + "/" + now + ".csv";
                // 新規作成する（既存のファイルに追記しない）
                // 文字コード
                var encode = System.Text.Encoding.GetEncoding("shift_jis");
                using (var sw = new System.IO.StreamWriter(filename, false, encode))
                {
                    sw.WriteLine("日付,出発地,到着地,金額,片道・往復,金額,領収書,備考,履歴番号");
                    foreach (var p in parseData)
                    {
                        if (p.UsageType != 0x01)
                        {
                            continue;
                        }
                        sw.WriteLine($"{p.Year}/{p.Month}/{p.Day},{p.InStationStr},{p.OutStationStr},{p.Charge},片道,{p.Charge},,,{p.HistoryNum}");
                    }
                }
            }
            catch (Exception ex)
            {
                // ファイルを開くのに失敗したときエラーメッセージを表示
                textBox.Text = ex.Message;
                playerNG.Play();
            }
        }

    } // class MainWindow
} // namespace SuicaApp
