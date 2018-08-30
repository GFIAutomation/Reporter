using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
namespace UnitTestProject1
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            IWebDriver mydriver = new FirefoxDriver();
            try
            {
                mydriver.Navigate().GoToUrl("http://www.google.com");
                mydriver.Quit();
            }
            catch (AssertInconclusiveException)
            {
                foreach (var process in Process.GetProcessesByName("firefox.exe"))
                {
                    process.Kill();
                }
                mydriver.Quit();
            };
        }
    }
}
