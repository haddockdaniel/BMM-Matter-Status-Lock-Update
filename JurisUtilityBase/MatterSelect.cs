using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace JurisUtilityBase
{
    public partial class MatterSelect : Form
    {
        public MatterSelect(string singleClient, JurisUtility JU)
        {
            InitializeComponent();
            client = singleClient;
            _jurisUtility = JU;
            dataGridView1.DataSource = null;
            string sql = "select dbo.jfn_FormatMatterCode(matcode) + '    ' + matreportingname as Matter from matter inner join client on clisysnbr = matclinbr where dbo.jfn_formatclientcode(clicode) = '" + client + "'";
            DataSet ds = _jurisUtility.RecordsetFromSQL(sql);
            dataGridView1.DataSource = ds.Tables[0];
            dataGridView1.Columns[0].Width = 280;
        }

        private string client;
        private JurisUtility _jurisUtility;
        public DataGridView dgv_copy = null;
        public int numOfRows = 0;

        private void buttonBack_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonPrint_Click(object sender, EventArgs e)
        {
            //get selected matters and send to DGV on main form
            dgv_copy = new DataGridView();
            dgv_copy = GetSelectedDataRows();
            this.Hide();
        }

        public  DataGridView GetSelectedDataRows()
        {
            foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                numOfRows++;
            try
            {
                if (dgv_copy.Columns.Count == 0)
                {
                    foreach (DataGridViewColumn dgvc in dataGridView1.Columns)
                    {
                        dgv_copy.Columns.Add(dgvc.Clone() as DataGridViewColumn);
                    }
                }

                DataGridViewRow row = new DataGridViewRow();

                foreach (DataGridViewRow r in dataGridView1.SelectedRows)
                {
                    row = (DataGridViewRow)r.Clone();
                    int intColIndex = 0;
                    foreach (DataGridViewCell cell in r.Cells)
                    {
                        row.Cells[intColIndex].Value = cell.Value;
                        intColIndex++;
                    }
                    dgv_copy.Rows.Add(row);
                }
                dgv_copy.AllowUserToAddRows = false;
                dgv_copy.Refresh();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            return dgv_copy;

        }
    }
}
