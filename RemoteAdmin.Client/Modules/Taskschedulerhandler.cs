using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using SysTask = System.Threading.Tasks.Task;
using Ts = Microsoft.Win32.TaskScheduler;
using TsTask = Microsoft.Win32.TaskScheduler.Task;
using TsService = Microsoft.Win32.TaskScheduler.TaskService;
using TsFolder = Microsoft.Win32.TaskScheduler.TaskFolder;
using SharedTaskState = RemoteAdmin.Shared.Enums.TaskState;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Client.Handlers
{
    /// <summary>
    /// Handles Task Scheduler operations on the client side
    /// </summary>
    public static class TaskSchedulerHandler
    {
        public static async SysTask HandleGetScheduledTasks(SslStream stream)
        {
            try
            {
                Console.WriteLine("[INFO] Getting scheduled tasks...");

                var tasks = GetAllScheduledTasks();

                var response = new GetScheduledTasksResponseMessage
                {
                    Tasks = tasks
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                Console.WriteLine($"[INFO] Sent {tasks.Count} scheduled tasks to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting scheduled tasks: {ex.Message}");

                var response = new GetScheduledTasksResponseMessage
                {
                    Tasks = new List<ScheduledTask>()
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async SysTask HandleCreateScheduledTask(SslStream stream, CreateScheduledTaskMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Creating scheduled task: {message.Task.Name}");

                bool success = CreateTask(message.Task, out string errorMessage);

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    Operation = TaskOperation.Create
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully created task: {message.Task.Name}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to create task: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Creating scheduled task: {ex.Message}");

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Operation = TaskOperation.Create
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async SysTask HandleDeleteScheduledTask(SslStream stream, DeleteScheduledTaskMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Deleting scheduled task: {message.TaskPath}");

                bool success = DeleteTask(message.TaskPath, out string errorMessage);

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    Operation = TaskOperation.Delete
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully deleted task: {message.TaskPath}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to delete task: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Deleting scheduled task: {ex.Message}");

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Operation = TaskOperation.Delete
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async SysTask HandleToggleScheduledTask(SslStream stream, ToggleScheduledTaskMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] {(message.Enable ? "Enabling" : "Disabling")} scheduled task: {message.TaskPath}");

                bool success = ToggleTask(message.TaskPath, message.Enable, out string errorMessage);

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    Operation = message.Enable ? TaskOperation.Enable : TaskOperation.Disable
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully {(message.Enable ? "enabled" : "disabled")} task: {message.TaskPath}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to toggle task: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Toggling scheduled task: {ex.Message}");

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Operation = message.Enable ? TaskOperation.Enable : TaskOperation.Disable
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async SysTask HandleRunScheduledTask(SslStream stream, RunScheduledTaskMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Running scheduled task: {message.TaskPath}");

                bool success = RunTask(message.TaskPath, out string errorMessage);

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage,
                    Operation = TaskOperation.Run
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully ran task: {message.TaskPath}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to run task: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Running scheduled task: {ex.Message}");

                var response = new ScheduledTaskOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Operation = TaskOperation.Run
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async SysTask HandleExportScheduledTask(SslStream stream, ExportScheduledTaskMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Exporting scheduled task: {message.TaskPath}");

                bool success = ExportTask(message.TaskPath, out string taskXml, out string errorMessage);

                var response = new ExportScheduledTaskResponseMessage
                {
                    Success = success,
                    TaskXml = taskXml,
                    ErrorMessage = errorMessage
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully exported task: {message.TaskPath}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to export task: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exporting scheduled task: {ex.Message}");

                var response = new ExportScheduledTaskResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        private static List<ScheduledTask> GetAllScheduledTasks()
        {
            var tasks = new List<ScheduledTask>();

            try
            {
                using (TsService ts = new TsService())
                {
                    GetTasksFromFolder(ts.RootFolder, tasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting scheduled tasks: {ex.Message}");
            }

            return tasks;
        }

        private static void GetTasksFromFolder(TsFolder folder, List<ScheduledTask> tasks)
        {
            try
            {
                // Get tasks in current folder
                foreach (Microsoft.Win32.TaskScheduler.Task task in folder.Tasks)
                {
                    try
                    {
                        tasks.Add(ConvertToScheduledTask(task));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Converting task {task.Name}: {ex.Message}");
                    }
                }

                // Recursively get tasks from subfolders
                foreach (TsFolder subFolder in folder.SubFolders)
                {
                    try
                    {
                        GetTasksFromFolder(subFolder, tasks);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Reading folder {subFolder.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading task folder: {ex.Message}");
            }
        }

        private static ScheduledTask ConvertToScheduledTask(Microsoft.Win32.TaskScheduler.Task task)
        {
            var scheduledTask = new ScheduledTask
            {
                Name = task.Name,
                Path = task.Path,
                State = ConvertTaskState(task.State),
                LastRunTime = task.LastRunTime == DateTime.MinValue ? null : task.LastRunTime,
                NextRunTime = task.NextRunTime == DateTime.MinValue ? null : task.NextRunTime,
                LastTaskResult = task.LastTaskResult,
                Enabled = task.Enabled,
                Triggers = new List<TaskTrigger>(),
                Actions = new List<Shared.TaskAction>()
            };

            // Get task definition details
            if (task.Definition != null)
            {
                scheduledTask.Author = task.Definition.RegistrationInfo?.Author ?? "Unknown";
                scheduledTask.Description = task.Definition.RegistrationInfo?.Description ?? "";
                scheduledTask.RunAsUser = task.Definition.Principal?.UserId ?? "Unknown";
                scheduledTask.Hidden = task.Definition.Settings?.Hidden ?? false;

                // Convert triggers
                foreach (var trigger in task.Definition.Triggers)
                {
                    try
                    {
                        scheduledTask.Triggers.Add(ConvertTrigger(trigger));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Converting trigger: {ex.Message}");
                    }
                }

                // Convert actions
                foreach (var action in task.Definition.Actions)
                {
                    try
                    {
                        scheduledTask.Actions.Add(ConvertAction(action));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Converting action: {ex.Message}");
                    }
                }
            }

            return scheduledTask;
        }

        private static TaskState ConvertTaskState(Microsoft.Win32.TaskScheduler.TaskState state)
        {
            return state switch
            {
                Microsoft.Win32.TaskScheduler.TaskState.Disabled => TaskState.Disabled,
                Microsoft.Win32.TaskScheduler.TaskState.Queued => TaskState.Queued,
                Microsoft.Win32.TaskScheduler.TaskState.Ready => TaskState.Ready,
                Microsoft.Win32.TaskScheduler.TaskState.Running => TaskState.Running,
                _ => TaskState.Unknown
            };
        }

        private static TaskTrigger ConvertTrigger(Ts.Trigger trigger)
        {
            var taskTrigger = new TaskTrigger
            {
                Type = ConvertTriggerType(trigger),
                Enabled = trigger.Enabled,
                StartBoundary = trigger.StartBoundary == DateTime.MinValue ? null : trigger.StartBoundary,
                EndBoundary = trigger.EndBoundary == DateTime.MinValue ? null : trigger.EndBoundary,
                Details = GetTriggerDetails(trigger)
            };

            return taskTrigger;
        }

        private static TriggerType ConvertTriggerType(Ts.Trigger trigger)
        {
            return trigger switch
            {
                Ts.DailyTrigger => TriggerType.Daily,
                Ts.WeeklyTrigger => TriggerType.Weekly,
                Ts.MonthlyTrigger => TriggerType.Monthly,
                Ts.MonthlyDOWTrigger => TriggerType.MonthlyDOW,
                Ts.TimeTrigger => TriggerType.Time,
                Ts.BootTrigger => TriggerType.Boot,
                Ts.LogonTrigger => TriggerType.Logon,
                Ts.IdleTrigger => TriggerType.Idle,
                Ts.EventTrigger => TriggerType.Event,
                Ts.RegistrationTrigger => TriggerType.Registration,
                Ts.SessionStateChangeTrigger => TriggerType.SessionStateChange,
                _ => TriggerType.Custom
            };
        }

        private static string GetTriggerDetails(Ts.Trigger trigger)
        {
            try
            {
                return trigger switch
                {
                    Ts.DailyTrigger dt => $"Daily, every {dt.DaysInterval} day(s) at {dt.StartBoundary:HH:mm}",
                    Ts.WeeklyTrigger wt => $"Weekly on {wt.DaysOfWeek} at {wt.StartBoundary:HH:mm}",
                    Ts.MonthlyTrigger mt => $"Monthly on day {string.Join(", ", mt.DaysOfMonth)} at {mt.StartBoundary:HH:mm}",
                    Ts.TimeTrigger tt => $"One time at {tt.StartBoundary:yyyy-MM-dd HH:mm}",
                    Ts.BootTrigger => "At system startup",
                    Ts.LogonTrigger lt => string.IsNullOrEmpty(lt.UserId) ? "At logon" : $"At logon of {lt.UserId}",
                    Ts.IdleTrigger => "When computer is idle",
                    Ts.EventTrigger et => $"On event: {et.Subscription}",
                    Ts.RegistrationTrigger => "When task is registered",
                    Ts.SessionStateChangeTrigger sst => $"On session state change: {sst.StateChange}",
                    _ => "Custom trigger"
                };
            }
            catch
            {
                return "Unknown trigger details";
            }
        }

        private static Shared.TaskAction ConvertAction(Microsoft.Win32.TaskScheduler.Action action)
        {
            var taskAction = new Shared.TaskAction
            {
                Type = ConvertActionType(action)
            };

            if (action is Ts.ExecAction execAction)
            {
                taskAction.Path = execAction.Path;
                taskAction.Arguments = execAction.Arguments;
                taskAction.WorkingDirectory = execAction.WorkingDirectory;
            }

            return taskAction;
        }

        private static ActionType ConvertActionType(Microsoft.Win32.TaskScheduler.Action action)
        {
            return action switch
            {
                Ts.ExecAction => ActionType.Execute,
                Ts.ComHandlerAction => ActionType.ComHandler,
                Ts.EmailAction => ActionType.SendEmail,
                Ts.ShowMessageAction => ActionType.ShowMessage,
                _ => ActionType.Execute
            };
        }

        private static bool CreateTask(ScheduledTask task, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                using (Ts.TaskService ts = new Ts.TaskService())
                {
                    // Create the task definition
                    Ts.TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = task.Description;
                    td.RegistrationInfo.Author = task.Author ?? Environment.UserName;

                    // Add triggers
                    foreach (var trigger in task.Triggers)
                    {
                        AddTrigger(td, trigger);
                    }

                    // Add actions
                    foreach (var action in task.Actions)
                    {
                        if (action.Type == ActionType.Execute && !string.IsNullOrEmpty(action.Path))
                        {
                            td.Actions.Add(new Ts.ExecAction(action.Path, action.Arguments, action.WorkingDirectory));
                        }
                    }

                    // Settings
                    td.Settings.Enabled = task.Enabled;
                    td.Settings.Hidden = task.Hidden;
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;

                    td.Principal.UserId = task.RunAsUser;
                    td.Principal.RunLevel = task.RunWithHighest
                        ? Ts.TaskRunLevel.Highest
                        : Ts.TaskRunLevel.LUA;

                    td.Principal.LogonType = task.RunOnlyWhenLoggedOn
                        ? Ts.TaskLogonType.InteractiveToken
                        : Ts.TaskLogonType.Password;

                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(task.Name, td);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to create task: {ex.Message}";
                return false;
            }
        }

        private static void AddTrigger(Ts.TaskDefinition td, TaskTrigger trigger)
        {
            Ts.Trigger newTrigger = trigger.Type switch
            {
                TriggerType.Daily => new Ts.DailyTrigger { DaysInterval = 1 },
                TriggerType.Weekly => new Ts.WeeklyTrigger { WeeksInterval = 1, DaysOfWeek = Ts.DaysOfTheWeek.Monday },
                TriggerType.Monthly => new Ts.MonthlyTrigger { DaysOfMonth = new int[] { 1 } },
                TriggerType.Time => new Ts.TimeTrigger(),
                TriggerType.Boot => new Ts.BootTrigger(),
                TriggerType.Logon => new Ts.LogonTrigger(),
                TriggerType.Idle => new Ts.IdleTrigger(),
                _ => new Ts.TimeTrigger()
            };

            newTrigger.Enabled = trigger.Enabled;

            if (trigger.StartBoundary.HasValue)
            {
                newTrigger.StartBoundary = trigger.StartBoundary.Value;
            }

            if (trigger.EndBoundary.HasValue)
            {
                newTrigger.EndBoundary = trigger.EndBoundary.Value;
            }

            td.Triggers.Add(newTrigger);
        }

        private static bool DeleteTask(string taskPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                using (Ts.TaskService ts = new Ts.TaskService())
                {
                    ts.RootFolder.DeleteTask(taskPath, false);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to delete task: {ex.Message}";
                return false;
            }
        }

        private static bool ToggleTask(string taskPath, bool enable, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                using (Ts.TaskService ts = new Ts.TaskService())
                {
                    var task = ts.GetTask(taskPath);
                    if (task != null)
                    {
                        task.Enabled = enable;
                        return true;
                    }
                    else
                    {
                        errorMessage = "Task not found";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to {(enable ? "enable" : "disable")} task: {ex.Message}";
                return false;
            }
        }

        private static bool RunTask(string taskPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                using (Ts.TaskService ts = new Ts.TaskService())
                {
                    var task = ts.GetTask(taskPath);
                    if (task != null)
                    {
                        task.Run();
                        return true;
                    }
                    else
                    {
                        errorMessage = "Task not found";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to run task: {ex.Message}";
                return false;
            }
        }

        private static bool ExportTask(string taskPath, out string taskXml, out string errorMessage)
        {
            taskXml = null;
            errorMessage = null;

            try
            {
                using (Ts.TaskService ts = new Ts.TaskService())
                {
                    var task = ts.GetTask(taskPath);
                    if (task != null)
                    {
                        taskXml = task.Xml;
                        return true;
                    }
                    else
                    {
                        errorMessage = "Task not found";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to export task: {ex.Message}";
                return false;
            }
        }
    }
}