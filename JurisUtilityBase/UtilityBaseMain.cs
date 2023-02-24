using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Globalization;
using Gizmox.Controls;
using JDataEngine;
using JurisAuthenticator;
using JurisUtilityBase.Properties;
using System.Data.OleDb;
using Microsoft.VisualBasic.FileIO;

namespace JurisUtilityBase
{
    public partial class UtilityBaseMain : Form
    {
        #region Private  members

        private JurisUtility _jurisUtility;

        #endregion

        #region Public properties

        public string CompanyCode { get; set; }

        public string JurisDbName { get; set; }

        public string JBillsDbName { get; set; }
        private List<ErrorLog> errorList = new List<ErrorLog>();



        #endregion

        #region Constructor

        public UtilityBaseMain()
        {
            InitializeComponent();
            _jurisUtility = new JurisUtility();
        }

        #endregion

        #region Public methods


        public void LoadCompanies()
        {
            var companies = _jurisUtility.Companies.Cast<object>().Cast<Instance>().ToList();
            //            listBoxCompanies.SelectedIndexChanged -= listBoxCompanies_SelectedIndexChanged;
            listBoxCompanies.ValueMember = "Code";
            listBoxCompanies.DisplayMember = "Key";
            listBoxCompanies.DataSource = companies;
            //            listBoxCompanies.SelectedIndexChanged += listBoxCompanies_SelectedIndexChanged;
            var defaultCompany = companies.FirstOrDefault(c => c.Default == Instance.JurisDefaultCompany.jdcJuris);
            if (companies.Count > 0)
            {
                listBoxCompanies.SelectedItem = defaultCompany ?? companies[0];
            }
        }

        #endregion

        #region MainForm events

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBoxLock.ClearItems();
            comboBoxLock.Items.Add("-    Leave Setting as is");
            comboBoxLock.Items.Add("0    Allow Time and Expense");
            comboBoxLock.Items.Add("1    Disallow Time, Allow Expense");
            comboBoxLock.Items.Add("2    Allow Time, Disallow Expense");
            comboBoxLock.Items.Add("3    Disallow Time and Expense");
            comboBoxLock.SelectedIndex = 0;

            comboBoxStatus.ClearItems();
            comboBoxStatus.Items.Add("-    Leave Setting as is");
            comboBoxStatus.Items.Add("O    Open");
            comboBoxStatus.Items.Add("C    Closed");
            comboBoxStatus.Items.Add("F    Final Bill Sent - Ready to Close");
            comboBoxStatus.SelectedIndex = 0;





        }

        private void listBoxCompanies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_jurisUtility.DbOpen)
            {
                _jurisUtility.CloseDatabase();
            }
            CompanyCode = "Company" + listBoxCompanies.SelectedValue;
            _jurisUtility.SetInstance(CompanyCode);
            JurisDbName = _jurisUtility.Company.DatabaseName;
            JBillsDbName = "JBills" + _jurisUtility.Company.Code;
            _jurisUtility.OpenDatabase();
            if (_jurisUtility.DbOpen)
            {
                ///GetFieldLengths();
            }

            string CliIndex2;
            cbClient.ClearItems();
            string SQLCli2 = "select dbo.jfn_formatclientcode(clicode) + '   ' +  clireportingname as Client from Client order by dbo.jfn_formatclientcode(clicode)";
            DataSet myRSCli2 = _jurisUtility.RecordsetFromSQL(SQLCli2);

            if (myRSCli2.Tables[0].Rows.Count == 0)
                cbClient.SelectedIndex = 0;
            else
            {
                foreach (DataRow dr in myRSCli2.Tables[0].Rows)
                {
                    CliIndex2 = dr["Client"].ToString();
                    cbClient.Items.Add(CliIndex2);
                }
            }

        }



        #endregion

        #region Private methods

        private string lockValue = "";
        private string statusValue = "";

        private void DoDaFix()
        {
            //get selections from status and lock
                lockValue = this.comboBoxLock.GetItemText(this.comboBoxLock.SelectedItem).Split(' ')[0];
                statusValue = this.comboBoxStatus.GetItemText(this.comboBoxStatus.SelectedItem).Split(' ')[0];
            if (passesValidation())
            {
                //update matters one at a time
                errorList.Clear();
                if (radioButtonAllMats.Checked)
                {
                    //get all matters
                    string sql = "select matsysnbr, dbo.jfn_FormatMatterCode(MatCode) from matter inner join client on clisysnbr = matclinbr where dbo.jfn_FormatClientCode(clicode) = '" + singleClient + "'";
                    DataSet ds = _jurisUtility.RecordsetFromSQL(sql);
                    List<int> mats = new List<int>();
                    foreach (DataRow dd in ds.Tables[0].Rows)
                        errorList.Add(processSingleMatter(Convert.ToInt32(dd[0].ToString()), dd[1].ToString()));
                }
                else
                {
                    foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                    {
                        //go through each matter, get its sysnbr and test if it has a balance
                        string value1 = r.Cells[0].Value.ToString();
                        string currentMat = value1.Split(' ')[0];
                        int matsys = getMatSysNbr(singleClient, currentMat);
                        errorList.Add(processSingleMatter(matsys, currentMat));
                    }
                }
                string allErrors = "";
                foreach (ErrorLog xx in errorList)
                    allErrors = allErrors + xx.message;

                if (string.IsNullOrEmpty(allErrors))
                    MessageBox.Show("Process completed without error", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                {
                    DialogResult dr = MessageBox.Show("Some matters were not able to be updated. Show details?", "Runtime Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (dr == DialogResult.Yes)
                    {
                        string message = "";
                        foreach (ErrorLog el in errorList)
                            message = message + el.message;
                        ErrorDisplay ed = new ErrorDisplay(message);
                        ed.Show();

                    }
                    errorList.Clear();
                }

            }
        }


        private bool passesValidation()
        {
            if (string.IsNullOrEmpty(singleClient))
            {
                MessageBox.Show("Please select a Client To Continue", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (lockValue.Equals("-") && statusValue.Equals("-"))
            {
                MessageBox.Show("Lock or Status must be changed for the utility to execute", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (radioButtonSelectMats.Checked)
            {
                int rows = 0;
                //see if they selected any matters
                foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                {
                    rows++;
                }
                if (rows == 0)
                {
                    MessageBox.Show("At least 1 matter must be chosen when using \"Specific Matters from Client\"", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                //see is any of those matters have balances if they want to close them
                if (statusValue.Equals("C"))
                {
                    foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                    {
                        //go through each matter, get its sysnbr and test if it has a balance
                        string value1 = r.Cells[0].Value.ToString();
                        string currentMat = value1.Split(' ')[0];
                        int matsys = getMatSysNbr(singleClient, currentMat);
                        checkForBalances(matsys);
                    }
                    //show error log then clear it
                    if (errorList.Count > 0) //we have at least 1 matter with a balance
                    {
                        DialogResult dr = MessageBox.Show("At least 1 Matter has a balance and cannot be closed. Show details?", "Selection Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                        if (dr == DialogResult.Yes)
                        {
                            string message = "";
                            foreach (ErrorLog el in errorList)
                                message = message + el.message;
                            ErrorDisplay ed = new ErrorDisplay(message);
                            ed.Show();
                        }
                        return false;
                    }
                    errorList.Clear();
                }
            }
            return true;
        }


        private ErrorLog processSingleMatter(int matsys, String currentMat)
        {
            string sql = "";
            ErrorLog el = new ErrorLog();
            el.client = "";
            el.matter = "";
            el.message = "";
            //get current status to see what we need to do
            sql = "select matstatusflag from matter where matsysnbr = " + matsys.ToString();
            DataSet ds = _jurisUtility.RecordsetFromSQL(sql);
            string oldStat = "";
            foreach (DataRow dd in ds.Tables[0].Rows)
                oldStat = dd[0].ToString();

            switch (statusValue)
            {
                case "-": //simply update the lock unless the matter is closed
                    if (!oldStat.Equals("C") && !oldStat.Equals("P"))
                    {
                        sql = "update matter set MatLockFlag = " + lockValue + " where matsysnbr = " + matsys.ToString();
                        _jurisUtility.ExecuteNonQuery(0, sql);
                    }
                    else
                        el.message = "Matter: " + currentMat + " cannot have its lock changed as it is closed and closed matters must have a lock code of 3" + "\r" + "\n";
                    break;
                case "O"://see if the old status was C 
                    if (oldStat.Equals("C") || oldStat.Equals("P"))
                    {
                        if (lockValue.Equals("-"))
                            sql = "update matter set MatStatusFlag = 'O', MatDateClosed = '01/01/1900' where matsysnbr = " + matsys.ToString();
                        else
                            sql = "update matter set MatStatusFlag = 'O', MatLockFlag = " + lockValue + ", MatDateClosed = '01/01/1900' where matsysnbr = " + matsys.ToString();
                        _jurisUtility.ExecuteNonQuery(0, sql);
                    }
                    else //it was already open (O or F)
                    {
                        if (lockValue.Equals("-"))
                            sql = "update matter set MatStatusFlag = 'O' where matsysnbr = " + matsys.ToString();
                        else
                            sql = "update matter set MatStatusFlag = 'O', MatLockFlag = " + lockValue + " where matsysnbr = " + matsys.ToString();
                        _jurisUtility.ExecuteNonQuery(0, sql);
                    }
                    break;
                case "C": //we already validated that we can close the matters
                    sql = "update matter set MatStatusFlag = 'C', MatLockFlag = 3, MatDateClosed = getdate() where matsysnbr = " + matsys.ToString();
                    _jurisUtility.ExecuteNonQuery(0, sql);
                    break;
                case "F"://see if the old status was C
                    if (oldStat.Equals("C") || oldStat.Equals("P"))
                    {
                        if (lockValue.Equals("-"))
                            sql = "update matter set MatStatusFlag = 'F', MatDateClosed = '01/01/1900' where matsysnbr = " + matsys.ToString();
                        else
                            sql = "update matter set MatStatusFlag = 'F', MatLockFlag = " + lockValue + ", MatDateClosed = '01/01/1900' where matsysnbr = " + matsys.ToString();
                        _jurisUtility.ExecuteNonQuery(0, sql);
                    }
                    else //it was already open (O or F)
                    {
                        if (lockValue.Equals("-"))
                            sql = "update matter set MatStatusFlag = 'F' where matsysnbr = " + matsys.ToString();
                        else
                            sql = "update matter set MatStatusFlag = 'F', MatLockFlag = " + lockValue + " where matsysnbr = " + matsys.ToString();
                        _jurisUtility.ExecuteNonQuery(0, sql);
                    }
                    break;
                default:

                break;
            }
            return el;
        }

        private int getMatSysNbr(string clicode, string matcode)
        {
            int matsys = 0;
            string sql = "";
            sql = "select matsysnbr from matter " +
                "inner join client on clisysnbr = matclinbr " +
                " where dbo.jfn_FormatClientCode(clicode) = '" + clicode + "' and dbo.jfn_FormatMatterCode(MatCode) = '" + matcode + "'";

            DataSet ds = new DataSet();
            ds = _jurisUtility.RecordsetFromSQL(sql);
            if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) // no matsys found
                return 0;
            else
            {
                matsys = Convert.ToInt32(ds.Tables[0].Rows[0][0].ToString());
                return matsys;
            }
        }


        private void checkForBalances(int matsysnbr) //false means some balance exists
        {
            string sql = "";
            sql =
                "  select dbo.jfn_FormatClientCode(clicode) as clicode, dbo.jfn_FormatMatterCode(MatCode) as matcode, cast(sum(ppd) as money) as ppd, cast(sum(UT) as money) as UT, cast(sum(UE) as money) as UE, cast(sum(AR) as money) as AR, cast(sum(Trust) as money) as Trust " +
                "from( " +
                "select matsysnbr as matsys, MatPPDBalance as ppd, 0 as UT, 0 as UE, 0 as AR, 0 as Trust " +
                  " from matter " +
                  " where MatPPDBalance <> 0 and matsysnbr = " + matsysnbr +
                  " union all " +
                   " select utmatter as matsys, 0 as ppd, sum(utamount) as UT, 0 as UE, 0 as AR, 0 as Trust " +
                  " from unbilledtime where utmatter = " + matsysnbr +
                  " group by utmatter " +
                  " having sum(utamount) <> 0 " +
                  " union all " +
                  " select uematter as matsys, 0 as ppd, 0 as UT, sum(ueamount) as UE, 0 as AR, 0 as Trust " +
                 " from unbilledexpense where uematter = " + matsysnbr +
                  "  group by uematter " +
                " having sum(ueamount) <> 0 " +
                 " union all " +
                "  select armmatter as matsys, 0 as ppd, 0 as UT, 0 as UE, sum(ARMBalDue) as AR, 0 as Trust " +
                "  from armatalloc where armmatter = " + matsysnbr +
                "  group by armmatter " +
                "  having sum(ARMBalDue) <> 0 " +
                "  union all " +
                "  select tamatter as matsys, 0 as ppd, 0 as UT, 0 as UE, 0 as AR, sum(TABalance) as Trust " +
                 " from trustaccount where tamatter = " + matsysnbr +
                "  group by tamatter " +
                "  having sum(TABalance) <> 0) hhg " +
                " inner join matter on hhg.matsys = matsysnbr " +
                " inner join client on clisysnbr = matclinbr " +
                "  group by dbo.jfn_FormatClientCode(clicode), dbo.jfn_FormatMatterCode(MatCode) " +
                "  having sum(ppd) <> 0 or sum(UT)  <> 0 or sum(UE)  <> 0 or sum(AR)  <> 0 or sum(Trust) <> 0";
            int woot = 0;
            DataSet ds = new DataSet();
            ds = _jurisUtility.RecordsetFromSQL(sql);
            if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                woot++; //not used
            else
            {
                ErrorLog er = new ErrorLog();
                er.client = ds.Tables[0].Rows[0][0].ToString();
                er.matter = ds.Tables[0].Rows[0][1].ToString();
                er.message = "Cannot close matter " + er.client + "/" + er.matter + " because balance(s) exist. See below for more detail: \r\n" +
                    "Prepaid Balance: " + ds.Tables[0].Rows[0][2].ToString() + "\r\n" +
                    "Unbilled Time Balance: " + ds.Tables[0].Rows[0][3].ToString() + "\r\n" +
                    "Unbilled Expense Balance: " + ds.Tables[0].Rows[0][4].ToString() + "\r\n" +
                    "A/R Balance: " + ds.Tables[0].Rows[0][5].ToString() + "\r\n" +
                    "Trust Balance: " + ds.Tables[0].Rows[0][6].ToString() + "\r\n" + "\r\n"
                    + "------------------------------------------------" + "\r\n" + "\r\n";
                errorList.Add(er);
            }
        }


        private bool VerifyFirmName()
        {
            //    Dim SQL     As String
            //    Dim rsDB    As ADODB.Recordset
            //
            //    SQL = "SELECT CASE WHEN SpTxtValue LIKE '%firm name%' THEN 'Y' ELSE 'N' END AS Firm FROM SysParam WHERE SpName = 'FirmName'"
            //    Cmd.CommandText = SQL
            //    Set rsDB = Cmd.Execute
            //
            //    If rsDB!Firm = "Y" Then
            return true;
            //    Else
            //        VerifyFirmName = False
            //    End If

        }

        private bool FieldExistsInRS(DataSet ds, string fieldName)
        {

            foreach (DataColumn column in ds.Tables[0].Columns)
            {
                if (column.ColumnName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }


        private static bool IsDate(String date)
        {
            try
            {
                DateTime dt = DateTime.Parse(date);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNumeric(object Expression)
        {
            double retNum;

            bool isNum = Double.TryParse(Convert.ToString(Expression), System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
            return isNum;
        }

        private void WriteLog(string comment)
        {
            var sql =
                string.Format("Insert Into UtilityLog(ULTimeStamp,ULWkStaUser,ULComment) Values('{0}','{1}', '{2}')",
                    DateTime.Now, GetComputerAndUser(), comment);
            _jurisUtility.ExecuteNonQueryCommand(0, sql);
        }

        private string GetComputerAndUser()
        {
            var computerName = Environment.MachineName;
            var windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var userName = (windowsIdentity != null) ? windowsIdentity.Name : "Unknown";
            return computerName + "/" + userName;
        }

        /// <summary>
        /// Update status bar (text to display and step number of total completed)
        /// </summary>
        /// <param name="status">status text to display</param>
        /// <param name="step">steps completed</param>
        /// <param name="steps">total steps to be done</param>
        private void UpdateStatus(string status, long step, long steps)
        {
            labelCurrentStatus.Text = status;

            if (steps == 0)
            {
                progressBar.Value = 0;
                labelPercentComplete.Text = string.Empty;
            }
            else
            {
                double pctLong = Math.Round(((double)step / steps) * 100.0);
                int percentage = (int)Math.Round(pctLong, 0);
                if ((percentage < 0) || (percentage > 100))
                {
                    progressBar.Value = 0;
                    labelPercentComplete.Text = string.Empty;
                }
                else
                {
                    progressBar.Value = percentage;
                    labelPercentComplete.Text = string.Format("{0} percent complete", percentage);
                }
            }
        }

        private void DeleteLog()
        {
            string AppDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            string filePathName = Path.Combine(AppDir, "VoucherImportLog.txt");
            if (File.Exists(filePathName + ".ark5"))
            {
                File.Delete(filePathName + ".ark5");
            }
            if (File.Exists(filePathName + ".ark4"))
            {
                File.Copy(filePathName + ".ark4", filePathName + ".ark5");
                File.Delete(filePathName + ".ark4");
            }
            if (File.Exists(filePathName + ".ark3"))
            {
                File.Copy(filePathName + ".ark3", filePathName + ".ark4");
                File.Delete(filePathName + ".ark3");
            }
            if (File.Exists(filePathName + ".ark2"))
            {
                File.Copy(filePathName + ".ark2", filePathName + ".ark3");
                File.Delete(filePathName + ".ark2");
            }
            if (File.Exists(filePathName + ".ark1"))
            {
                File.Copy(filePathName + ".ark1", filePathName + ".ark2");
                File.Delete(filePathName + ".ark1");
            }
            if (File.Exists(filePathName))
            {
                File.Copy(filePathName, filePathName + ".ark1");
                File.Delete(filePathName);
            }

        }



        private void LogFile(string LogLine)
        {
            string AppDir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            string filePathName = Path.Combine(AppDir, "VoucherImportLog.txt");
            using (StreamWriter sw = File.AppendText(filePathName))
            {
                sw.WriteLine(LogLine);
            }
        }
        #endregion



        public string singleClient = "";

        private void cbClient_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbClient.SelectedIndex > 0)
                singleClient = this.cbClient.GetItemText(this.cbClient.SelectedItem).Split(' ')[0];
            dataGridView1.DataSource = null;
            string sql = "select dbo.jfn_FormatMatterCode(matcode) + '    ' + matreportingname as Matter from matter inner join client on clisysnbr = matclinbr where dbo.jfn_formatclientcode(clicode) = '" + singleClient + "'";
            DataSet ds = _jurisUtility.RecordsetFromSQL(sql);
            dataGridView1.DataSource = ds.Tables[0];
            dataGridView1.Columns[0].Width = 280;
        }

 

        private void radioButtonAllMats_CheckedChanged(object sender, EventArgs e)
        {
            dataGridView1.Visible = radioButtonSelectMats.Checked;
        }

        private void radioButtonSelectMats_CheckedChanged(object sender, EventArgs e)
        {
            dataGridView1.Visible = radioButtonSelectMats.Checked;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            DoDaFix();
        }

        private void buttonReport_Click_1(object sender, EventArgs e)
        {
            System.Environment.Exit(0);
        }
    }
}
