using MotionControl.Domain.Entities;

namespace MotionControl.Control.Services;

public sealed class AlarmPollingService(
    Machine machine,
    ControllerRuntimeState controllerRuntimeState,
    CommandFeedbackRuntimeState commandFeedbackRuntimeState)
{
    public Task PollAsync(CancellationToken cancellationToken = default)
    {
        var controllerStatus = controllerRuntimeState.LastControllerStatus;

        if (controllerStatus is null || !controllerStatus.IsConnected)
        {
            if (machine.UpsertAlarm("SYS-CONTROLLER-DISCONNECTED", "Controller not connected", "System", "Communication", "Error"))
            {
                commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "Alarm",
                    Status = "Raised",
                    Message = "Controller disconnect alarm raised"
                });
            }
        }
        else if (machine.ClearAlarm("SYS-CONTROLLER-DISCONNECTED"))
        {
            commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "Alarm",
                Status = "Cleared",
                Message = "Controller disconnect alarm cleared"
            });
        }

        if (controllerStatus?.HasOfflineSlave == true)
        {
            var offlineMessage = $"EtherCAT has {controllerStatus.OfflineSlaveCount} offline slave(s)";
            if (machine.UpsertAlarm("ECAT-SLAVE-OFFLINE", offlineMessage, "EtherCAT", "Network", "Warning"))
            {
                commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "Alarm",
                    Status = "Raised",
                    Message = offlineMessage
                });
            }
        }
        else if (machine.ClearAlarm("ECAT-SLAVE-OFFLINE"))
        {
            commandFeedbackRuntimeState.Add(new CommandFeedback
            {
                CommandName = "Alarm",
                Status = "Cleared",
                Message = "EtherCAT offline slave alarm cleared"
            });
        }

        if (controllerStatus is not null)
        {
            foreach (var slave in controllerStatus.Slaves)
            {
                var offlineCode = $"ECAT-SLAVE-{slave.SlaveNo:00}-OFFLINE";
                var alarmCode = $"ECAT-SLAVE-{slave.SlaveNo:00}-ALARM";

                if (!slave.IsOnline)
                {
                    var offlineMessage = $"EtherCAT slave {slave.SlaveNo} {slave.Name} offline";
                    if (machine.UpsertAlarm(offlineCode, offlineMessage, slave.Name, "EtherCAT", "Warning"))
                    {
                        commandFeedbackRuntimeState.Add(new CommandFeedback
                        {
                            CommandName = "Alarm",
                            Status = "Raised",
                            Message = offlineMessage
                        });
                    }
                }
                else if (machine.ClearAlarm(offlineCode))
                {
                    commandFeedbackRuntimeState.Add(new CommandFeedback
                    {
                        CommandName = "Alarm",
                        Status = "Cleared",
                        Message = $"EtherCAT slave {slave.SlaveNo} offline alarm cleared"
                    });
                }

                if (slave.HasAlarm)
                {
                    var slaveAlarmMessage = $"EtherCAT slave {slave.SlaveNo} {slave.Name} alarm active";
                    if (machine.UpsertAlarm(alarmCode, slaveAlarmMessage, slave.Name, "EtherCAT", "Error"))
                    {
                        commandFeedbackRuntimeState.Add(new CommandFeedback
                        {
                            CommandName = "Alarm",
                            Status = "Raised",
                            Message = slaveAlarmMessage
                        });
                    }
                }
                else if (machine.ClearAlarm(alarmCode))
                {
                    commandFeedbackRuntimeState.Add(new CommandFeedback
                    {
                        CommandName = "Alarm",
                        Status = "Cleared",
                        Message = $"EtherCAT slave {slave.SlaveNo} alarm cleared"
                    });
                }
            }
        }

        foreach (var axis in machine.Axes)
        {
            var code = $"AXIS-{axis.ControllerAxisNo:00}-ALARM";
            var message = $"{axis.Name} axis alarm active";
            if (axis.HasAlarm)
            {
                if (machine.UpsertAlarm(code, message, axis.Name, "Motion", "Error"))
                {
                    commandFeedbackRuntimeState.Add(new CommandFeedback
                    {
                        CommandName = "Alarm",
                        AxisNo = axis.ControllerAxisNo,
                        Status = "Raised",
                        Message = message
                    });
                }
            }
            else if (machine.ClearAlarm(code))
            {
                commandFeedbackRuntimeState.Add(new CommandFeedback
                {
                    CommandName = "Alarm",
                    AxisNo = axis.ControllerAxisNo,
                    Status = "Cleared",
                    Message = $"{axis.Name} axis alarm cleared"
                });
            }
        }

        return Task.CompletedTask;
    }
}
