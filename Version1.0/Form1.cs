﻿using Dapper;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Updater1._1;

namespace Version1._0
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CheckUpdate();
        }

        public async void CheckUpdate()
        {

            int b = -1;
            await Task.Run(() =>
            {
                bool retry;
                bool error;
                do
                {
                    error = false;
                    retry = false;
                    try
                    {
                        RunWithTimeout(() =>
                        {
                            using (var conn = new SqlConnection(""))
                            {
                                var parameters = new DynamicParameters();
                                parameters.Add("@foo", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
                                parameters.Add("@Version", "1.0");
                                parameters.Add("@Product", "Test");
                                conn.Execute("[dbo].spCheckVersion", parameters, commandType: CommandType.StoredProcedure);

                                b = parameters.Get<int>("@foo");

                            }
                        }, 2000, ref error);
                        
                    }
                    catch (Exception ex)
                    {
                        error = true;
                    }
                    if (b == -1 || error)
                    {
                        DialogResult dialogResult = MessageBox.Show("Threre was an error connecting to the SQL server and check for updates.\n Do you want to retry?", "SQL connection error!", MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                        {
                            Console.WriteLine("trying again");
                            retry = true;
                        }
                        else if(dialogResult == DialogResult.No)
                        {
                            Console.WriteLine("Not trying again");
                            return;
                        }
                    }
                    
                } while (retry);
            });


            if (b == -1)
                return;

            if (b == 0)
            {
                MessageBox.Show("You have the latest version");
            }
            else
            {
                if (MessageBox.Show("This version is of the application is old.\nInitialize update?", "You have an old version!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    await Task.Run(() =>
                    {
                        bool retry;
                        bool error;
                        do
                        {
                            error = false;
                            retry = false;
                            try
                            {
                                RunWithTimeout(() =>
                                {

                                    string newName;
                                    using (var conn = new SqlConnection(""))
                                    {
                                        DynamicParameters _param = new DynamicParameters();
                                        _param.Add("@Product", "test");
                                        _param.Add("@newName", "", DbType.String, ParameterDirection.Output);

                                        conn.Execute("[dbo].spGetNewVersion @Product, @newName output", _param);

                                        newName = _param.Get<string>("newName");
                                    }
                                    Update(newName);

                                }, 10000, ref error);
                            }
                            catch (Exception)
                            {
                                error = true;
                            }
                            if (error && MessageBox.Show("Threre was an error connecting to the SQL server and get information.\n Do you want to retry?", "SQL connection error!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            {
                                retry = true;
                            }
                        } while (retry);
                    });


                }
            }

        }

        private async void Update(string newVersion)
        {

            this.Invoke(new MethodInvoker(() =>
            {
                new UpdateScreen().Show();
            }));
            
            string path = Application.StartupPath;
            string filename = Path.GetFileName(Application.ExecutablePath);
            string PID = Process.GetCurrentProcess().Id.ToString();
            string newFilename = newVersion;
            bool retry;
            do
            {
                retry = false;
                try
                {
                    AzureFileDownload("Updater.exe", "updates", path);

                    Process.Start("Updater.exe", $" \"{path}\" \"{filename}\" {PID} \"{newFilename}\"");
                }
                catch (Exception)
                {
                    if (MessageBox.Show("An error occured while downloading the Updater.\n do you want to retry?", "Critical error!!!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        retry = true;
                    }
                    
                }
            } while (retry);
        }

        public static void AzureFileDownload(string fileName, string containerName, string path)
        {
            string mystrconnectionString = "";//<------------------ enter your key here!

            CloudStorageAccount mycloudStorageAccount = CloudStorageAccount.Parse(mystrconnectionString);
            CloudBlobClient myBlob = mycloudStorageAccount.CreateCloudBlobClient();

            CloudBlobContainer mycontainer = myBlob.GetContainerReference(containerName);
            CloudBlockBlob myBlockBlob = mycontainer.GetBlockBlobReference(fileName);

            // provide the location of the file need to be downloaded          
            Stream fileupd = File.OpenWrite(path + "\\"  + "Updater.exe");
            myBlockBlob.DownloadToStream(fileupd);

            fileupd.Dispose();
        }
        static void RunWithTimeout(Action entryPoint, int timeout, ref bool cancel)
        {
            var thread = new Thread(() => entryPoint()) { IsBackground = true };

            thread.Start();

            if (!thread.Join(timeout))
            {
                cancel = true;
                thread.Abort();
            }

        }
    }
}
