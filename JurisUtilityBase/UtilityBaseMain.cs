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
            try
            {
                //get selections from status and lock
                lockValue = this.comboBoxLock.GetItemText(this.comboBoxLock.SelectedItem).Split(' ')[0];
                statusValue = this.comboBoxStatus.GetItemText(this.comboBoxStatus.SelectedItem).Split(' ')[0];
                if (passesValidation())
                {
                    //used to hold matter details that cannot be closed
                    string sql = @"create table ##TempBals 
                                (Client varchar(12),
                                Matter varchar(12), 
                                PpdBalance decimal(15,2), 
                                WIPFees decimal(15,2), 
                                WIPExpense decimal(15,2), 
                                UnpostedTime int, 
                                UnpostedExpense int,
                                UnpostedVouchers int, 
                                OpenVouchers int, 
                                OpenBills int,
                                ARBalance decimal(15,2),
                                TrustBalance decimal(15,2),
                                ReadytoClose int)";
                    _jurisUtility.ExecuteNonQuery(0, sql);
                    //update matters one at a time
                    if (radioButtonAllMats.Checked)
                    {
                        //get all matters
                        sql = "select matsysnbr, dbo.jfn_FormatMatterCode(MatCode) from matter inner join client on clisysnbr = matclinbr where dbo.jfn_FormatClientCode(clicode) = '" + singleClient + "'";
                        DataSet ds = _jurisUtility.RecordsetFromSQL(sql);
                        List<int> mats = new List<int>();
                        foreach (DataRow dd in ds.Tables[0].Rows)
                        {
                            if (statusValue.Equals("C")) //they are trying to close a matter
                            {
                                //see if its in the readytoclose = 0 category
                                checkForBalances(Convert.ToInt32(dd[0].ToString()));
                                sql = "select ReadytoClose from ##TempBals where Matter = '" + dd[1].ToString() + "'";
                                DataSet qq = _jurisUtility.RecordsetFromSQL(sql);
                                foreach (DataRow ii in qq.Tables[0].Rows)
                                {
                                    if (ii[0].ToString().Equals("1"))//we can close it
                                        processSingleMatter(Convert.ToInt32(dd[0].ToString()), dd[1].ToString());
                                }
                            }
                            else
                                processSingleMatter(Convert.ToInt32(dd[0].ToString()), dd[1].ToString());
                        }
                    }
                    else
                    {
                        foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                        {
                            try
                            {
                                string value1 = r.Cells[0].Value.ToString();
                                string currentMat = value1.Split(' ')[0];
                                int matsys = getMatSysNbr(singleClient, currentMat);
                                if (statusValue.Equals("C")) //they are trying to close a matter
                                {
                                    //see if its in the readytoclose = 0 category

                                    checkForBalances(matsys);
                                    sql = "select ReadytoClose from ##TempBals where Matter = '" + currentMat + "'";
                                    DataSet zz = _jurisUtility.RecordsetFromSQL(sql);
                                    foreach (DataRow ii in zz.Tables[0].Rows)
                                    {
                                        if (ii[0].ToString().Equals("1"))//we can close it
                                            processSingleMatter(matsys, currentMat);
                                    }
                                }
                                else
                                    processSingleMatter(matsys, currentMat);
                            }
                            catch (Exception cc) { MessageBox.Show(cc.Message); }
                        }
                    }

                    int count = 0;
                    sql = "select count(*) as cd from ##TempBals where ReadytoClose = 0";
                    DataSet uh = _jurisUtility.RecordsetFromSQL(sql);

                    foreach (DataRow rg in uh.Tables[0].Rows)
                    {
                        count = Convert.ToInt32(rg[0].ToString());
                    }




                    if (count == 0)
                        MessageBox.Show("Process completed without error", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                    {
                        DialogResult dr = MessageBox.Show("Some matters have balances and cannot be closed. Show details?", "Runtime Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                        if (dr == DialogResult.Yes)
                        {
                            sql = "select * from ##TempBals where ReadytoClose = 0";
                            DataSet gg = _jurisUtility.RecordsetFromSQL(sql);

                            ReportDisplay rd = new ReportDisplay(gg.Tables[0]);
                            rd.Show();
                        }
                    }
                    sql = "drop table ##TempBals";
                    _jurisUtility.ExecuteNonQuery(0, sql);
                }
            }
            catch (Exception bb) { MessageBox.Show(bb.Message); }
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

            }
            return true;
        }


        private void processSingleMatter(int matsys, String currentMat)
        {
            string sql = "";
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


        private void checkForBalances(int matsysnbr) 
        {
            //put all of them in a table and see if any are ReadyToClose = 0
            string sql = @"insert into ##TempBals
                            (Client, Matter, PpdBalance,WIPFees,WIPExpense, UnpostedTime,UnpostedExpense,UnpostedVouchers,
                            OpenVouchers, OpenBills,ARBalance,TrustBalance,ReadytoClose )         
                        Select dbo.jfn_FormatClientCode(clicode) as Client, dbo.jfn_FormatMatterCode(MatCode) as Matter, ppd as PpdBalance, unbilledtime as WIPFees, 
                          unbilledexp as WIPExpense, unpostedtime as UnpostedTime, unpostedexp as UnpostedExpense, unpostedvouchers as UnpostedVouchers, openvouchers as OpenVouchers, 
                  openbills as OpenBills,  ar as ARBalance,  trust as TrustBalance,
                    case when wip <> 0.00 or UnbilledTime <> 0 or UnbilledExp <> 0 or UnpostedTime <> 0 or UnpostedExp <> 0 or UnpostedVouchers <> 0 or OpenVouchers <> 0 or openbills <> 0 or AR <> 0.00 or PPD <> 0.00 or trust <> 0.00 then 0 else 1 end as ReadytoClose 
                    from matter
                    inner join client on matclinbr=clisysnbr
                    inner join billto on matbillto=billtosysnbr
                    inner join employee on empsysnbr=billtobillingatty
                    inner join (select morigmat, max(case when ot=1 then empinitials else '' end) + max(case when ot=2 then  ' ' +  empinitials else '' end) + 
                    max(case when ot=3 then   ' ' +  empinitials else '' end) +  max(case when ot=4 then   ' ' +  empinitials else '' end) + max(case when ot=5 then   ' ' +  empinitials else '' end) as OrigTkpr
                    from (select morigmat, empinitials, rank() over (Partition by morigmat order by empinitials) as OT
                    from matorigatty inner join employee on morigatty=empsysnbr)MO
                    group by morigmat)MO on matsysnbr=morigmat
                    inner join (select matsysnbr as matsys, sum(unbilledtime) as UnbilledTime, sum(UnbilledExp) as UnbilledExp, sum(unpostedtime) as UnpostedTime, sum(unpostedexp) as UnpostedExp,
                    sum(unpostedvouchers) as UnpostedVouchers, sum(openVouchers) as OpenVouchers, sum(openBills) as OpenBills, sum(wipbalance) as WIp, sum(arbalance) as AR, sum(ppdbalance) as PPD, sum(trustbalance) as trust
                    from (select matsysnbr, 0 as UnbilledTime, 0 as UnbilledExp, 0 as UnpostedTime, 0 as UnpostedExp, 0 as UnpostedVouchers, 0 as OpenVouchers,0 as OpenBills, 0 as WIPBalance,0 as ARBalance, matppdbalance as PPDBalance, 0 as TrustBalance
                    from matter
                    union all select armmatter, 0, 0, 0, 0, 0, 0, count(armbillnbr) as OpenBills,  0 as WIp, sum(armbaldue) as ARBalance, 0 as PPD, 0 as Trust from armatalloc where armbaldue<>0 group by armmatter
                    union all select utmatter, sum(utamount), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 from unbilledtime group by utmatter union all
                    select uematter, 0, sum(ueamount), 0,0,0,0,0,0, 0, 0, 0 from unbilledexpense group by uematter
                    union all  select tbdmatter, 0,0, count(tbdid),0,0,0,0,0,0,0,0 from timebatchdetail  where tbdposted='N' and tbdid not in (select tbdid from timeentrylink) group by tbdmatter
                    union all  select mattersysnbr, 0,0, count(entryid),0,0,0,0,0,0,0,0 from timeentry where entrystatus<=6 group by mattersysnbr
                    union all  select ebdmatter, 0,0, 0,count(ebdid),0,0,0,0,0,0,0 from expbatchdetail where ebdposted='N' and ebdid not in (select ebdid from ExpenseEntryLink) group by ebdmatter
                    union all  select mattersysnbr, 0,0, 0,count(entryid),0,0,0,0,0,0,0 from expenseentry where entrystatus<=6  group by mattersysnbr
                    union all  select vbmmatter, 0,0, 0,0,count(vbdid) as VchCount,0,0,0,0,0,0  from voucherbatchmatdist inner join voucherbatchdetail on vbdbatch=vbmbatch and vbdrecnbr=vbmrecnbr where vbdposted='N' group by vbmmatter
                    union all  select vmmatter,0,0, 0,0,0, count(vmvoucher) as Vch,0,0,0,0,0 from voucher inner join vouchermatdist on vmvoucher=vchvouchernbr where vchstatus='O' and vmamount-vmamountpaid<>0 group by vmmatter 
                    union all  select tlmatter,0,0,0,0,0,0,0,0,0,0, sum(tlamount) from trustledger group by tlmatter) Mat group by matsysnbr)MatList on matsysnbr=matsys
			        where MatStatusFlag  not in ('P', 'C') and MatSysNbr = " + matsysnbr.ToString();

            _jurisUtility.ExecuteSql(0, sql);
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

        }

 

        private void radioButtonAllMats_CheckedChanged(object sender, EventArgs e)
        {
        }

        private int numOfMatters = 0;

        private void radioButtonSelectMats_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (radioButtonSelectMats.Checked)//show matter select dialog, see which matters they selected (and the number of them) and add to hidden dgv on this form for processing
                {
                    MatterSelect ms = new MatterSelect(singleClient, _jurisUtility);
                    ms.ShowDialog();
                    try
                    {
                        if (ms.numOfRows != 0)
                        {
                            numOfMatters = ms.numOfRows;
                            CopyDataGridView(ms.dgv_copy);
                            dataGridView1.SelectAll();
                            //MessageBox.Show(dataGridView1.SelectedRows.Count.ToString() + " : " + dataGridView1.Rows.Count.ToString() + " : " + numOfMatters.ToString());
                        }
                        else
                        {
                            MessageBox.Show("At least 1 matter must be chosen when using \"Specific Matters from Client\"", "Selection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            radioButtonAllMats.Checked = true;
                        }
                    }
                    catch (Exception ss) { MessageBox.Show(ss.Message); }
                    ms.Close();

                }
            }catch (Exception vv) { MessageBox.Show(vv.Message); }
        }


            private void CopyDataGridView(DataGridView dgv_org)
        {
            try
            {
                if (dataGridView1.Columns.Count == 0)
                {
                    foreach (DataGridViewColumn dgvc in dgv_org.Columns)
                    {
                        dataGridView1.Columns.Add(dgvc.Clone() as DataGridViewColumn);
                    }
                }

                DataGridViewRow row = new DataGridViewRow();

                for (int i = 0; i < dgv_org.Rows.Count; i++)
                {
                    row = (DataGridViewRow)dgv_org.Rows[i].Clone();
                    int intColIndex = 0;
                    foreach (DataGridViewCell cell in dgv_org.Rows[i].Cells)
                    {
                        row.Cells[intColIndex].Value = cell.Value;
                        intColIndex++;
                    }
                    dataGridView1.Rows.Add(row);
                }
                dataGridView1.AllowUserToAddRows = false;
                dataGridView1.Refresh();

            }
            catch (Exception ex)
            {
                MessageBox.Show("inner " + ex.Message);
            }
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
