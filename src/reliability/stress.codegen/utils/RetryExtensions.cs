// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace stress.codegen.utils
{
    internal static class RetryExtensions
    {
        public async static Task TryAsync(this Func<Task> asyncAction, int retryCount, int delay = 0)
        {
            //a list to aggregate excpetions caught on retry attempts
            List<Exception> innerExs = new List<Exception>();

            for (; retryCount >= 0;)
            {
                try
                {
                    //await the completion asyncAction
                    await asyncAction();

                    //if asyncAction failed due to exception we will jump down to the catch
                    //we only get here if asyncAction ran successfully so we break out of the loop
                    break;
                }
                catch (Exception e)
                {
                    //add the exception to the list of exceptions
                    innerExs.Add(e);

                    //if we don't have any retries left throw the aggregate exception
                    if (retryCount-- <= 0)
                    {
                        throw new AggregateException("The specified action failed {0} times.  See InnerExceptions for specific failures.", innerExs);
                    }

                    //else swallow the exception
                }

                //if a valid delay was specified delay before retry
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }
            }
        }

        public async static Task<T> TryAsync<T>(this Func<Task<T>> asyncAction, int retryCount, int delay = 0)
        {
            T ret = default(T);

            //a list to aggregate excpetions caught on retry attempts
            List<Exception> innerExs = new List<Exception>();

            for (; retryCount >= 0;)
            {
                try
                {
                    //await the completion asyncAction
                    ret = await asyncAction();

                    //if asyncAction failed due to exception we will jump down to the catch
                    //we only get here if asyncAction ran successfully so we break out of the loop
                    break;
                }
                catch (Exception e)
                {
                    //add the exception to the list of exceptions
                    innerExs.Add(e);

                    //if we don't have any retries left throw the aggregate exception
                    if (retryCount-- <= 0)
                    {
                        throw new AggregateException("The specified action failed {0} times.  See InnerExceptions for specific failures.", innerExs);
                    }

                    //else swallow the exception
                }

                //if a valid delay was specified delay before retry
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }
            }

            return ret;
        }

        //NOTE: if no delay is specified or an invalid delay is specified this method will exectute syncronously
        public async static Task TryAsync(this Action action, int retryCount, int delay = 0)
        {
            //a list to aggregate excpetions caught on retry attempts
            List<Exception> innerExs = new List<Exception>();

            for (; retryCount >= 0;)
            {
                try
                {
                    //run the action
                    action();

                    //if action failed due to exception we will jump down to the catch
                    //we only get here if action ran successfully so break out of the loop
                    break;
                }
                catch (Exception e)
                {
                    //add the exception to the list of exceptions
                    innerExs.Add(e);

                    //if we don't have any retries left throw the aggregate exception
                    if (retryCount-- <= 0)
                    {
                        throw new AggregateException("The specified action failed {0} times.  See InnerExceptions for specific failures.", innerExs);
                    }

                    //else swallow the exception
                }

                //if a valid delay was specified delay before retry
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }
            }
        }

        public static void Try(this Action action, int retryCount, int delay = 0)
        {
            //a list to aggregate excpetions caught on retry attempts
            List<Exception> innerExs = new List<Exception>();

            for (; retryCount >= 0;)
            {
                try
                {
                    //run the action
                    action();

                    //if action failed due to exception we will jump down to the catch
                    //we only get here if action ran successfully so break out of the loop
                    break;
                }
                catch (Exception e)
                {
                    //add the exception to the list of exceptions
                    innerExs.Add(e);

                    //if we don't have any retries left throw the aggregate exception
                    if (retryCount-- <= 0)
                    {
                        throw new AggregateException("The specified action failed {0} times.  See InnerExceptions for specific failures.", innerExs);
                    }

                    //else swallow the exception
                }

                //if a valid delay was specified delay before retry
                if (delay > 0)
                {
                    Task.Delay(delay).Wait();
                }
            }
        }
    }
}
