﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Management;

namespace DDD.Utility
{
    public class SysInfo
    {
        public string CpuID;
        public string MacAddress;
        public string DiskID;
        public string IpAddress;
        public string LoginUserName;
        public string ComputerName;
        public string SystemType;
        public string TotalPhysicalMemory; //单位：M
        private static SysInfo _instance;
        public static SysInfo Instance()
        {
            if (_instance == null)
                _instance = new SysInfo();
            return _instance;
        }
        protected SysInfo()
        {
            CpuID=GetCpuID();
            MacAddress=GetMacAddress();
            DiskID=GetDiskID();
            IpAddress=GetIPAddress();
            LoginUserName=GetUserName();
            SystemType=GetSystemType();
            TotalPhysicalMemory=GetTotalPhysicalMemory();
            ComputerName=GetComputerName();
        }
        #region CpuUsage类
        /// <summary>
        /// Defines an abstract base class for implementations of CPU usage counters.
        /// </summary>
        public abstract class CpuUsage
        {
            /// <summary>
            /// Creates and returns a CpuUsage instance that can be used to query the CPU time on this operating system.
            /// </summary>
            /// <returns>An instance of the CpuUsage class.</returns>
            /// <exception cref="NotSupportedException">This platform is not supported -or- initialization of the CPUUsage object failed.</exception>
            public static CpuUsage Create()
            {
                if (m_CpuUsage == null)
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                        m_CpuUsage = new CpuUsageNt();
                    else if (Environment.OSVersion.Platform == PlatformID.Win32Windows)
                        m_CpuUsage = new CpuUsage9x();
                    else
                        throw new NotSupportedException();
                }
                return m_CpuUsage;
            }
            /// <summary>
            /// Determines the current average CPU load.
            /// </summary>
            /// <returns>An integer that holds the CPU load percentage.</returns>
            /// <exception cref="NotSupportedException">One of the system calls fails. The CPU time can not be obtained.</exception>
            public abstract int Query();
            /// <summary>
            /// Holds an instance of the CPUUsage class.
            /// </summary>
            private static CpuUsage m_CpuUsage = null;
        }

        //------------------------------------------- win 9x ---------------------------------------

        /// <summary>
        /// Inherits the CPUUsage class and implements the Query method for Windows 9x systems.
        /// </summary>
        /// <remarks>
        /// <p>This class works on Windows 98 and Windows Millenium Edition.</p>
        /// <p>You should not use this class directly in your code. Use the CPUUsage.Create() method to instantiate a CPUUsage object.</p>
        /// </remarks>
        internal sealed class CpuUsage9x : CpuUsage
        {
            /// <summary>
            /// Initializes a new CPUUsage9x instance.
            /// </summary>
            /// <exception cref="NotSupportedException">One of the system calls fails.</exception>
            public CpuUsage9x()
            {
                try
                {
                    // start the counter by reading the value of the 'StartStat' key
                    RegistryKey startKey =  Registry.PerformanceData.OpenSubKey(@"PerfStats/StartStat", false);
                    if (startKey == null)
                        throw new NotSupportedException();
                    startKey.GetValue(@"KERNEL/CPUUsage");
                    startKey.Close();
                    // open the counter's value key
                    m_StatData = Registry.PerformanceData.OpenSubKey(@"PerfStats/StatData", false);
                    if (m_StatData == null)
                        throw new NotSupportedException();
                }
                catch (NotSupportedException e)
                {
                    throw e;
                }
                catch (Exception e)
                {
                    throw new NotSupportedException("Error while querying the system information.", e);
                }
            }
            /// <summary>
            /// Determines the current average CPU load.
            /// </summary>
            /// <returns>An integer that holds the CPU load percentage.</returns>
            /// <exception cref="NotSupportedException">One of the system calls fails. The CPU time can not be obtained.</exception>
            public override int Query()
            {
                try
                {
                    return (int)m_StatData.GetValue(@"KERNEL/CPUUsage");
                }
                catch (Exception e)
                {
                    throw new NotSupportedException("Error while querying the system information.", e);
                }
            }
            /// <summary>
            /// Closes the allocated resources.
            /// </summary>
            ~CpuUsage9x()
            {
                try
                {
                    m_StatData.Close();
                }
                catch { }
                // stopping the counter
                try
                {
                    RegistryKey stopKey = Registry.PerformanceData.OpenSubKey(@"PerfStats/StopStat", false);
                    stopKey.GetValue(@"KERNEL/CPUUsage", false);
                    stopKey.Close();
                }
                catch { }
            }
            /// <summary>Holds the registry key that's used to read the CPU load.</summary>
            private RegistryKey m_StatData;
        }

        //------------------------------------------- win nt ---------------------------------------

        /// <summary>
        /// Inherits the CPUUsage class and implements the Query method for Windows NT systems.
        /// </summary>
        /// <remarks>
        /// <p>This class works on Windows NT4, Windows 2000, Windows XP, Windows .NET Server and higher.</p>
        /// <p>You should not use this class directly in your code. Use the CPUUsage.Create() method to instantiate a CPUUsage object.</p>
        /// </remarks>
        internal sealed class CpuUsageNt : CpuUsage
        {
            /// <summary>
            /// Initializes a new CpuUsageNt instance.
            /// </summary>
            /// <exception cref="NotSupportedException">One of the system calls fails.</exception>
            public CpuUsageNt()
            {
                byte[] timeInfo = new byte[32];		// SYSTEM_TIME_INFORMATION structure
                byte[] perfInfo = new byte[312];	// SYSTEM_PERFORMANCE_INFORMATION structure
                byte[] baseInfo = new byte[44];		// SYSTEM_BASIC_INFORMATION structure
                int ret;
                // get new system time
                ret = NtQuerySystemInformation(SYSTEM_TIMEINFORMATION, timeInfo, timeInfo.Length, IntPtr.Zero);
                if (ret != NO_ERROR)
                    throw new NotSupportedException();
                // get new CPU's idle time
                ret = NtQuerySystemInformation(SYSTEM_PERFORMANCEINFORMATION, perfInfo, perfInfo.Length, IntPtr.Zero);
                if (ret != NO_ERROR)
                    throw new NotSupportedException();
                // get number of processors in the system
                ret = NtQuerySystemInformation(SYSTEM_BASICINFORMATION, baseInfo, baseInfo.Length, IntPtr.Zero);
                if (ret != NO_ERROR)
                    throw new NotSupportedException();
                // store new CPU's idle and system time and number of processors
                oldIdleTime = BitConverter.ToInt64(perfInfo, 0); // SYSTEM_PERFORMANCE_INFORMATION.liIdleTime
                oldSystemTime = BitConverter.ToInt64(timeInfo, 8); // SYSTEM_TIME_INFORMATION.liKeSystemTime
                processorCount = baseInfo[40];
            }
            /// <summary>
            /// Determines the current average CPU load.
            /// </summary>
            /// <returns>An integer that holds the CPU load percentage.</returns>
            /// <exception cref="NotSupportedException">One of the system calls fails. The CPU time can not be obtained.</exception>
            public override int Query()
            {
                byte[] timeInfo = new byte[32];		// SYSTEM_TIME_INFORMATION structure
                byte[] perfInfo = new byte[312];	// SYSTEM_PERFORMANCE_INFORMATION structure
                double dbIdleTime, dbSystemTime;
                int ret;
                // get new system time
                ret = NtQuerySystemInformation(SYSTEM_TIMEINFORMATION, timeInfo, timeInfo.Length, IntPtr.Zero);
                if (ret != NO_ERROR)
                    throw new NotSupportedException();
                // get new CPU's idle time
                ret = NtQuerySystemInformation(SYSTEM_PERFORMANCEINFORMATION, perfInfo, perfInfo.Length, IntPtr.Zero);
                if (ret != NO_ERROR)
                    throw new NotSupportedException();
                // CurrentValue = NewValue - OldValue
                dbIdleTime = BitConverter.ToInt64(perfInfo, 0) - oldIdleTime;
                dbSystemTime = BitConverter.ToInt64(timeInfo, 8) - oldSystemTime;
                // CurrentCpuIdle = IdleTime / SystemTime
                if (dbSystemTime != 0)
                    dbIdleTime = dbIdleTime / dbSystemTime;
                // CurrentCpuUsage% = 100 - (CurrentCpuIdle * 100) / NumberOfProcessors
                dbIdleTime = 100.0 - dbIdleTime * 100.0 / processorCount + 0.5;
                // store new CPU's idle and system time
                oldIdleTime = BitConverter.ToInt64(perfInfo, 0); // SYSTEM_PERFORMANCE_INFORMATION.liIdleTime
                oldSystemTime = BitConverter.ToInt64(timeInfo, 8); // SYSTEM_TIME_INFORMATION.liKeSystemTime
                return (int)dbIdleTime;
            }
            /// <summary>
            /// NtQuerySystemInformation is an internal Windows function that retrieves various kinds of system information.
            /// </summary>
            /// <param name="dwInfoType">One of the values enumerated in SYSTEM_INFORMATION_CLASS, indicating the kind of system information to be retrieved.</param>
            /// <param name="lpStructure">Points to a buffer where the requested information is to be returned. The size and structure of this information varies depending on the value of the SystemInformationClass parameter.</param>
            /// <param name="dwSize">Length of the buffer pointed to by the SystemInformation parameter.</param>
            /// <param name="returnLength">Optional pointer to a location where the function writes the actual size of the information requested.</param>
            /// <returns>Returns a success NTSTATUS if successful, and an NTSTATUS error code otherwise.</returns>
            [DllImport("ntdll", EntryPoint = "NtQuerySystemInformation")]
            private static extern int NtQuerySystemInformation(int dwInfoType, byte[] lpStructure, int dwSize, IntPtr returnLength);
            /// <summary>Returns the number of processors in the system in a SYSTEM_BASIC_INFORMATION structure.</summary>
            private const int SYSTEM_BASICINFORMATION = 0;
            /// <summary>Returns an opaque SYSTEM_PERFORMANCE_INFORMATION structure.</summary>
            private const int SYSTEM_PERFORMANCEINFORMATION = 2;
            /// <summary>Returns an opaque SYSTEM_TIMEOFDAY_INFORMATION structure.</summary>
            private const int SYSTEM_TIMEINFORMATION = 3;
            /// <summary>The value returned by NtQuerySystemInformation is no error occurred.</summary>
            private const int NO_ERROR = 0;
            /// <summary>Holds the old idle time.</summary>
            private long oldIdleTime;
            /// <summary>Holds the old system time.</summary>
            private long oldSystemTime;
            /// <summary>Holds the number of processors in the system.</summary>
            private double processorCount;
        }
        #endregion

        /// <summary>
        /// 获得Cpu使用率
        /// </summary>
        /// <returns>返回使用率</returns>
        public static int GetCpuUsage()
        {
            return CpuUsage.Create().Query();
        }

        string GetCpuID()
        {
            try
            {
                //获取CPU序列号代码
                string cpuInfo = "";//cpu序列号
                ManagementClass mc = new ManagementClass("Win32_Processor");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    cpuInfo = mo.Properties["ProcessorId"].Value.ToString();
                }
                moc = null;
                mc = null;
                return cpuInfo;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }
        string GetMacAddress()
        {
            try
            {
                //获取网卡硬件地址
                string mac = "";
                ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    if ((bool)mo["IPEnabled"] == true)
                    {
                        mac = mo["MacAddress"].ToString();
                        break;
                    }
                }
                moc = null;
                mc = null;
                return mac;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }
        string GetIPAddress()
        {
            try
            {
                //获取IP地址
                string st = "";
                ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    if ((bool)mo["IPEnabled"] == true)
                    {
                        //st=mo["IpAddress"].ToString();
                        System.Array ar;
                        ar = (System.Array)(mo.Properties["IpAddress"].Value);
                        st = ar.GetValue(0).ToString();
                        break;
                    }
                }
                moc = null;
                mc = null;
                return st;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }

        string GetDiskID()
        {
            try
            {
                //获取硬盘ID
                String HDid = "";
                ManagementClass mc = new ManagementClass("Win32_DiskDrive");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    HDid = (string)mo.Properties["Model"].Value;
                }
                moc = null;
                mc = null;
                return HDid;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }

        /// <summary>
        /// 操作系统的登录用户名
        /// </summary>
        /// <returns></returns>
        string GetUserName()
        {
            try
            {
                string st = "";
                ManagementClass mc = new ManagementClass("Win32_ComputerSystem");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    st = mo["UserName"].ToString();
                }
                moc = null;
                mc = null;
                return st;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }


        /// <summary>
        /// PC类型
        /// </summary>
        /// <returns></returns>
        string GetSystemType()
        {
            try
            {
                string st = "";
                ManagementClass mc = new ManagementClass("Win32_ComputerSystem");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    st = mo["SystemType"].ToString();
                }
                moc = null;
                mc = null;
                return st;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }

        }

        /// <summary>
        /// 物理内存
        /// </summary>
        /// <returns></returns>
        string GetTotalPhysicalMemory()
        {
            try
            {
                string st = "";
                ManagementClass mc = new ManagementClass("Win32_ComputerSystem");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    st = mo["TotalPhysicalMemory"].ToString();
                }
                moc = null;
                mc = null;
                return st;
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        string GetComputerName()
        {
            try
            {
                return System.Environment.GetEnvironmentVariable("ComputerName");
            }
            catch
            {
                return "unknow";
            }
            finally
            {
            }
        }
    }
}
