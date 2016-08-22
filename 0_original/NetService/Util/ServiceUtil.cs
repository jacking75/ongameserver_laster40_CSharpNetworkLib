using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;

namespace NetService.Util
{

    public static class ServiceUtil
    {
        /// <summary>
        /// 서비스의 현재 상태를 구한다.
        /// </summary>
        /// <param name="serviceName">서비스 이름</param>
        /// <returns>
        /// StartPending, Running, PausePending, Paused, StopPending, Stopped, ContinuePending 
        /// </returns>
        public static string GetStatus(string serviceName)
        {
            string status;
            try
            {
                // http://msdn.microsoft.com/en-us/library/system.serviceprocess.servicecontrollerstatus.aspx
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;
                status = sc.Status.ToString();
            }
            catch (Exception e)
            {
                status = string.Empty;
            }
            return status;
        }

        /// <summary>
        /// 서비스 시작
        /// </summary>
        /// <param name="serviceName">서비스 이름</param>
        /// <returns></returns>
        public static bool Start(string serviceName)
        {
            try
            {
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;
                sc.Start();
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 서비스 멈춤
        /// </summary>
        /// <param name="serviceName">서비스 이름</param>
        public static void Stop(string serviceName)
        {
            try
            {
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;
                sc.Stop();
            }
            catch (Exception e)
            {
            }
        }
    }
}
