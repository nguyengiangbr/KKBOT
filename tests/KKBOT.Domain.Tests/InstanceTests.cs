using KKBOT.Domain.Enums;
using KKBOT.Domain.Models;
using Xunit;

namespace KKBOT.Domain.Tests;

public class InstanceTests
{
    [Fact]
    public void NewInstance_ShouldBeInCreatedState()
    {
        var instance = new Instance
        {
            Key = "test-inst",
            DisplayName = "Test Instance",
            DeviceId = Guid.NewGuid(),
            ModuleId = Guid.NewGuid(),
            ProfileId = Guid.NewGuid()
        };

        Assert.Equal(InstanceState.Created, instance.State);
    }

    [Fact]
    public void SetState_TransitionsCorrectly()
    {
        var instance = new Instance
        {
            Key = "test-inst",
            DisplayName = "Test Instance"
        };

        instance.SetState(InstanceState.Ready);
        Assert.Equal(InstanceState.Ready, instance.State);
        Assert.Equal(InstanceState.Created, instance.PreviousState);
        Assert.NotEmpty(instance.StateTransitions);
    }
}
