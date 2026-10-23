using HornViet.Domain.Chat.Entities;
using HornViet.Domain.Chat.Enums;
using HornViet.Domain.SharedKernel;

namespace HornViet.Domain.UnitTests.Chat;

public class ChatConversationTests
{
    [Fact]
    public void NewConversation_StartsWithBot()
    {
        var c = new ChatConversation(userId: 7, guestKey: null);

        Assert.Equal(ChatConversationStatus.Bot, c.Status);
        Assert.False(c.IsHandedOff);
        Assert.Null(c.GuestKey);
    }

    [Fact]
    public void GuestConversation_RequiresKey()
    {
        Assert.Throws<ArgumentException>(() => new ChatConversation(userId: null, guestKey: null));

        var c = new ChatConversation(userId: null, guestKey: "abc");
        Assert.Equal("abc", c.GuestKey);
    }

    [Fact]
    public void RequestStaff_ThenAssign_GoesWaitingThenWithStaff()
    {
        var c = new ChatConversation(1, null);

        c.RequestStaff();
        Assert.Equal(ChatConversationStatus.WaitingStaff, c.Status);

        c.AssignStaff(staffUserId: 2);
        Assert.Equal(ChatConversationStatus.WithStaff, c.Status);
        Assert.Equal(2, c.StaffUserId);
    }

    [Fact]
    public void CustomerMessages_CountUnreadOnlyAfterHandoff()
    {
        var c = new ChatConversation(1, null);

        c.AddMessage(ChatSender.Customer, "giá bao nhiêu");
        Assert.Equal(0, c.StaffUnread);

        c.RequestStaff();
        c.AddMessage(ChatSender.Customer, "alo");
        c.AddMessage(ChatSender.Customer, "có ai không");
        Assert.Equal(2, c.StaffUnread);

        c.MarkReadByStaff();
        Assert.Equal(0, c.StaffUnread);
    }

    [Fact]
    public void AssignStaff_OtherStaffNeedsTakeOver()
    {
        var c = new ChatConversation(1, null);
        c.AssignStaff(2);

        Assert.Throws<UserFriendlyException>(() => c.AssignStaff(3));

        c.AssignStaff(3, takeOver: true);
        Assert.Equal(3, c.StaffUserId);
    }

    [Fact]
    public void CancelStaffRequest_OnlyBeforeStaffJoins()
    {
        var c = new ChatConversation(1, null);
        c.RequestStaff();

        c.CancelStaffRequest();
        Assert.Equal(ChatConversationStatus.Bot, c.Status);

        c.AssignStaff(2);
        Assert.Throws<UserFriendlyException>(() => c.CancelStaffRequest());
    }

    [Fact]
    public void ClosedConversation_RejectsNewMessages()
    {
        var c = new ChatConversation(1, null);
        c.Close();

        Assert.True(c.IsClosed);
        Assert.Throws<UserFriendlyException>(() => c.AddMessage(ChatSender.Customer, "còn hàng không"));
        Assert.Throws<UserFriendlyException>(() => c.RequestStaff());
    }
}
