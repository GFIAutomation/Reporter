using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;

namespace reporter2
{
    class Program
    {   // Variables for reporting
        static SqlConnection conn = new SqlConnection("Data Source = gfitestlanding.database.windows.net;persistsecurityinfo=True;Database=testLanding;UID=daniel.domingues;Password=123Qwe,.-;");
        static DateTime test_date_start;
        static String test_status = String.Empty;
        static String build_execution_time = String.Empty;
        static String general_message = String.Empty;
        static Process process = new Process();
        static String test_name = String.Empty;
        static String build_status = String.Empty;
        static DateTime build_date_start, build_date_end;

        const int project_id = 2;
        static int report_id = 0;
        static bool report_started = false;
        static bool end_the_build = false;

        static void Main(string[] args)
        {
            // Add tests
            String[] tests = { @"C:\Users\claudio.costa\Documents\Reporter\UnitTestProject2\UnitTestProject2\bin\Debug\UnitTestProject2.dll" };
            TextWriterTraceListener myListener = new TextWriterTraceListener("Buildlog.txt", "myListener");
            bool test_started = false;

            foreach (String test in tests)
            {
                //* Create your Process
                Process process = new Process();
                process.StartInfo.FileName = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
                process.StartInfo.Arguments = test;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();
                while (!end_the_build)
                {
                    string err = String.Empty;
                    err = process.StandardError.ReadLine();
                    if(err == null)
                    {
                        Console.WriteLine("Build started");
                        build_date_start = DateTime.Now;
                        InitializeReport(build_date_start);
                        SelectReportId();
                        report_started = true;
                    }
                    else if (!err.Contains("provided was not found.") && !err.Contains("The test source file") && !report_started)
                    {
                        Console.WriteLine("Build started");
                        build_date_start = DateTime.Now;
                        InitializeReport(build_date_start);
                        SelectReportId();
                        report_started = true;
                    }
                    else
                    {
                        Console.WriteLine("Build failed");
                        Environment.Exit(0);
                    }

                    while (!process.StandardOutput.EndOfStream || !process.StandardError.EndOfStream)
                    {
                        Console.WriteLine("Reading line");
                        string output = process.StandardOutput.ReadLine();
                        string error = process.StandardError.ReadLine();

                        if (!test_started && report_started)
                        {
                            Console.WriteLine("Test started");
                            test_date_start = DateTime.Now;
                            test_started = true;
                        }

                        if (output.Contains("Passed "))
                        {
                            test_status = output.Remove(6);
                            test_name = output.Remove(1, 6);
                            if (build_status != "Failed")
                                build_status = "Passed";
                            InsertReportCollectionsRow(project_id, report_id, test_date_start, DateTime.Now, test_status, "Passed with success", test_name, "Tester");
                            test_started = false;
                            Console.WriteLine("Test passed and closed");
                        }
                        else if (output.Contains("Failed "))
                        {
                            test_status = output.Remove(6);
                            test_name = output.Remove(1, 6);
                            build_status = "Failed";
                            InsertReportCollectionsRow(project_id, report_id, test_date_start, DateTime.Now, test_status, "Unsuccessfully ran.", test_name, "Tester");
                            test_started = false;
                            Console.WriteLine("Test failed and closed");
                        }
                        if (output.Contains("Test execution time "))
                            build_execution_time = output;
                        if (output.Contains("Passed: "))
                            general_message = output;

                        Console.WriteLine("Waiting");
                    }
                    process.WaitForExit();
                    myListener.Flush();
                    build_date_end = DateTime.Now;
                    UpdateReport();
                    end_the_build = true;
                }
            }
        }

        static void InitializeReport(DateTime dt)
        {
            conn.Open();
            SqlCommand insert = new SqlCommand("INSERT INTO Report(date_start, date_end,status,general_message) VALUES " +
                "(@param1,@param2,@param3,@param4);", conn);
            insert.Parameters.AddWithValue("@param1", dt);
            insert.Parameters.AddWithValue("@param2", "");
            insert.Parameters.AddWithValue("@param3", "Running");
            insert.Parameters.AddWithValue("@param4", "Build is currently running.");
            insert.ExecuteNonQuery();
            conn.Close();
        }

        static void SelectReportId()
        {
            var datetimenow = build_date_start.ToString();
            SqlCommand select = new SqlCommand("SELECT id FROM Report WHERE date_start = @param1", conn);
            select.Parameters.AddWithValue("@param1", build_date_start);
            conn.Open();
            var reader = select.ExecuteReader();
            if (reader.Read())
            {
                report_id = int.Parse(String.Format("{0}", reader["id"]));
            }
            conn.Close();
        }
        static void InsertReportCollectionsRow(int project_id, int report_id, DateTime dtStart,
                                                DateTime dtEnd, String status, String GeneralMessage,
                                                String TestName, String Author)
        {
            conn.Open();
            SqlCommand insert = new SqlCommand("INSERT INTO ReportCollection(project_id, report_id,date_start,date_end, " +
                "status, general_message, test_name, author) VALUES " +
                "(@param1,@param2,@param3,@param4,@param5,@param6,@param7,@param8);", conn);
            insert.Parameters.AddWithValue("@param1", project_id);
            insert.Parameters.AddWithValue("@param2", report_id);
            insert.Parameters.AddWithValue("@param3", dtStart);
            insert.Parameters.AddWithValue("@param4", dtEnd);
            insert.Parameters.AddWithValue("@param5", status);
            insert.Parameters.AddWithValue("@param6", general_message);
            insert.Parameters.AddWithValue("@param7", TestName);
            insert.Parameters.AddWithValue("@param8", Author);
            insert.ExecuteNonQuery();
            conn.Close();
        }

        static int SelectPassedTests()
        {
            int passed_tests = 0;
            SqlCommand select = new SqlCommand("SELECT id FROM ReportCollection WHERE report_id = @param1 AND Status=@param2", conn);
            select.Parameters.AddWithValue("@param1", report_id);
            select.Parameters.AddWithValue("@param2", "Passed");
            var reader = select.ExecuteReader();
            while (reader.Read())
            {
                passed_tests += 1;
            }
            reader.Close();
            return passed_tests;
        }

        static int SelectTotalTests()
        {
            int total_tests = 0;
            SqlCommand select = new SqlCommand("SELECT id FROM ReportCollection WHERE report_id = @param1", conn);
            select.Parameters.AddWithValue("@param1", report_id);
            var reader = select.ExecuteReader();
            while (reader.Read())
            {
                total_tests += 1;
            }
            reader.Close();
            return total_tests;
        }

        static void UpdateReport()
        {
            conn.Open();
            int passed_tests = SelectPassedTests();
            int total_tests = SelectTotalTests();
            SqlCommand insert = new SqlCommand("UPDATE Report SET Status=@param1, date_end=@param2,general_message=@param3,pass_tests=@param4,total_tests=@param5 WHERE id =" + report_id, conn);
            insert.Parameters.AddWithValue("@param1", build_status);
            insert.Parameters.AddWithValue("@param2", build_date_end);
            insert.Parameters.AddWithValue("@param3", general_message);
            insert.Parameters.AddWithValue("@param4", passed_tests);
            insert.Parameters.AddWithValue("@param5", total_tests);
            insert.ExecuteNonQuery();
            conn.Close();
        }
    }
}
