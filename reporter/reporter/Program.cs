using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;

namespace reporter
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

        const int id_project = 2;
        static int id_build = 0;
        const string author = "Tester";
        static bool build_started = false;
        static bool end_the_build = false;

        static void Main(string[] args)
        {
            // Add tests
            String[] tests = { @"C:\Users\ivo.saraiva\Documents\Portal\Report\Reporter\UnitTestProject1\UnitTestProject1\bin\Debug\UnitTestProject1.dll" };
            TextWriterTraceListener myListener = new TextWriterTraceListener("Buildlog.txt", "myListener");
            bool test_started = false, testFailded = false;
            string p = "", e = "";
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
                    string err = process.StandardError.ReadLine();
                    if (!err.Contains("provided was not found.") && !err.Contains("The test source file") && !build_started)
                    {
                        Console.WriteLine("Build started");
                        build_date_start = DateTime.Now;
                        InitializeBuild(build_date_start);
                        SelectBuildId();
                        build_started = true;
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
                        p += "\n" + output;
                        e += "\n" + error;

                        if (!test_started && build_started)
                        {
                            Console.WriteLine("Test started");
                            test_date_start = DateTime.Now;
                            test_started = true;
                        }

                        if (testFailded && output.Contains("Error Message:"))
                        {
                            string error_message = process.StandardOutput.ReadLine();
                            Console.WriteLine(error_message);
                            testFailded = false;
                        }

                        if (output.Contains("Passed "))
                        {
                            test_status = output.Remove(6);
                            test_name = output.Remove(1, 6);
                            if (build_status != "Failed")
                                build_status = "Passed";
                            InsertTestBuildRow(id_project, id_build, test_date_start, DateTime.Now, test_status, "Passed with success", test_name);
                            test_started = false;
                            Console.WriteLine("Test passed and closed");
                        }
                        else if (output.Contains("Failed "))
                        {
                            test_status = output.Remove(6);
                            test_name = output.Remove(1, 6);
                            build_status = "Failed";
                            InsertTestBuildRow(id_project, id_build, test_date_start, DateTime.Now, test_status, "Unsuccessfully ran.", test_name);
                            test_started = false;
                            Console.WriteLine("Test failed and closed");
                        }
                        else if (output.Contains("Skipped "))
                        {
                            test_status = output.Remove(7);
                            test_name = output.Remove(1, 6);
                            build_status = "Skipped";
                            InsertTestBuildRow(id_project, id_build, test_date_start, DateTime.Now, test_status, "Skipped test.", test_name);
                            test_started = false;
                            Console.WriteLine("Test skipped and closed");
                        }
                        if (output.Contains("Test execution time "))
                            build_execution_time = output;
                        if (output.Contains("Passed: "))
                            general_message = output;

                        Console.WriteLine("Waiting");
                    }
                    Console.WriteLine(p);

                    Console.WriteLine("\nERROR \n-------------------");
                    Console.WriteLine(e);
                    process.WaitForExit();
                    myListener.Flush();
                    build_date_end = DateTime.Now;
                    UpdateReport();
                    end_the_build = true;
                }
            }
        }

        static void InitializeBuild(DateTime dt)
        {
            conn.Open();
            SqlCommand insert = new SqlCommand("INSERT INTO Build(date_start, date_end,status,general_message, id_project, tool_name) VALUES " +
                "(@param1,@param2,@param3,@param4,@param5,@param6);", conn);
            insert.Parameters.AddWithValue("@param1", dt);
            insert.Parameters.AddWithValue("@param2", "");
            insert.Parameters.AddWithValue("@param3", "Running");
            insert.Parameters.AddWithValue("@param4", "Build is currently running.");
            insert.Parameters.AddWithValue("@param5", id_project);
            insert.Parameters.AddWithValue("@param6", "Selenium");
            insert.ExecuteNonQuery();
            conn.Close();
        }

        static void SelectBuildId()
        {
            var datetimenow = build_date_start.ToString();
            SqlCommand select = new SqlCommand("SELECT id FROM Build WHERE date_start = @param1", conn);
            select.Parameters.AddWithValue("@param1", build_date_start);
            conn.Open();
            var reader = select.ExecuteReader();
            if (reader.Read())
            {
                id_build = int.Parse(String.Format("{0}", reader["id"]));
            }
            conn.Close();
        }

        static void InsertTestBuildRow(int project_id, int build_id, DateTime dtStart,
                                                DateTime dtEnd, String status, string generalMessage,
                                                string testName)
        {
            var sDuration = getDuration(dtEnd, dtStart);
            conn.Open();
            SqlCommand insert = new SqlCommand("INSERT INTO Tools_Test(id_project, id_build,date_start,date_end, " +
                "status, general_message, name, duration) VALUES " +
                "(@param1,@param2,@param3,@param4,@param5,@param6,@param7,@param8);", conn);
            insert.Parameters.AddWithValue("@param1", project_id);
            insert.Parameters.AddWithValue("@param2", build_id);
            insert.Parameters.AddWithValue("@param3", dtStart);
            insert.Parameters.AddWithValue("@param4", dtEnd);
            insert.Parameters.AddWithValue("@param5", status);
            insert.Parameters.AddWithValue("@param6", general_message);
            insert.Parameters.AddWithValue("@param7", testName);
            insert.Parameters.AddWithValue("@param8", sDuration);
            insert.ExecuteNonQuery();
            conn.Close();
        }

        static int SelectPassedTests()
        {
            int passed_tests = 0;
            SqlCommand select = new SqlCommand("SELECT id FROM Tools_Test WHERE id_build = @param1 AND Status=@param2", conn);
            select.Parameters.AddWithValue("@param1", id_build);
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
            SqlCommand select = new SqlCommand("SELECT id FROM Tools_Test WHERE id_build = @param1", conn);
            select.Parameters.AddWithValue("@param1", id_build);
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
            int skipped_tests = SelectSkippedTests();
            int failed_tests = SelectFailedTests();
            var sDuration = getDuration(build_date_end, build_date_start);

            SqlCommand insert = new SqlCommand("UPDATE Build SET status=@param1, date_end=@param2,general_message=@param3,pass_tests=@param4,total_tests=@param5, duration=@param6, skipped_tests=@param7, failed_tests=@param8 WHERE id =" + id_build, conn);
            insert.Parameters.AddWithValue("@param1", build_status);
            insert.Parameters.AddWithValue("@param2", build_date_end);
            insert.Parameters.AddWithValue("@param3", general_message);
            insert.Parameters.AddWithValue("@param4", passed_tests);
            insert.Parameters.AddWithValue("@param5", total_tests);
            insert.Parameters.AddWithValue("@param6", sDuration);
            insert.Parameters.AddWithValue("@param7", skipped_tests);
            insert.Parameters.AddWithValue("@param8", failed_tests);
            insert.ExecuteNonQuery();
            conn.Close();
        }

        private static int SelectSkippedTests()
        {
            int skipped_tests = 0;
            SqlCommand select = new SqlCommand("SELECT id FROM Tools_Test WHERE id_build = @param1 AND status=@param2", conn);
            select.Parameters.AddWithValue("@param1", id_build);
            select.Parameters.AddWithValue("@param2", "Skipped");
            var reader = select.ExecuteReader();
            while (reader.Read())
            {
                skipped_tests += 1;
            }
            reader.Close();
            return skipped_tests;

        }

        private static int SelectFailedTests()
        {
            int failed_tests = 0;
            SqlCommand select = new SqlCommand("SELECT id FROM Tools_Test WHERE id_build = @param1 AND status=@param2", conn);
            select.Parameters.AddWithValue("@param1", id_build);
            select.Parameters.AddWithValue("@param2", "Failed");
            var reader = select.ExecuteReader();
            while (reader.Read())
            {
                failed_tests += 1;
            }
            reader.Close();
            return failed_tests;

        }

        private static string getDuration(DateTime build_date_end, DateTime build_date_start)
        {
            var duration = build_date_end.Subtract(build_date_start);

            var sDuration = "";
            if (duration.Days != 0)
            {
                sDuration = String.Format(" {0} days,", duration.Days);
            }
            if (duration.Hours != 0)
            {
                sDuration += String.Format(" {0} hours,", duration.Hours);
            }
            if (duration.Minutes != 0)
            {
                sDuration += String.Format(" {0} minutes,", duration.Minutes);
            }
            if (duration.Seconds != 0)
            {
                sDuration += String.Format(" {0} seconds,", duration.Seconds);
            }
            if (duration.Milliseconds != 0)
            {
                sDuration += String.Format(" {0} ms", duration.Milliseconds);
            }

            return sDuration;
        }
    }


}
