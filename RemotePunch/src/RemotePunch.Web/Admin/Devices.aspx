<%@ Page Title="Devices" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Devices.aspx.cs" Inherits="RemotePunch.Web.Admin.DevicesPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Devices</h1>
    <p class="muted">
        Every punch records a fingerprint of the phone or computer it came from.
        Trusting one stops it being flagged; blocking one refuses its punches.
        A fingerprint is a correlation aid, not proof of identity - a new browser,
        a cleared profile or a system update can all change it.
    </p>

    <asp:PlaceHolder ID="phMessage" runat="server" Visible="false">
        <div class="alert alert-ok"><asp:Literal ID="litMessage" runat="server" /></div>
    </asp:PlaceHolder>

    <div class="card">
        <div class="table-wrap">
            <asp:Repeater ID="rptDevices" runat="server" OnItemCommand="rptDevices_ItemCommand">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>Employee</th><th>Device</th><th>Fingerprint</th>
                                <th class="num">Punches</th><th>First seen</th><th>Last seen</th>
                                <th>State</th><th></th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td><%# PageHelpers.TextOrDash(Eval("EmployeeName")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("Label")) %></td>
                        <td class="mono"><%# ShortFingerprint(Eval("Fingerprint")) %></td>
                        <td class="num"><%# Eval("PunchCount") %></td>
                        <td class="small"><%# LocalTime(Eval("FirstSeenUtc")) %></td>
                        <td class="small"><%# LocalTime(Eval("LastSeenUtc")) %></td>
                        <td><%# StateBadge(Container.DataItem) %></td>
                        <td class="nowrap">
                            <asp:LinkButton runat="server" CssClass="btn btn-ghost btn-sm" CommandName="trust"
                                CommandArgument='<%# Eval("DeviceId") %>' Text="Trust" />
                            <asp:LinkButton runat="server" CssClass="btn btn-ghost btn-sm" CommandName="untrust"
                                CommandArgument='<%# Eval("DeviceId") %>' Text="Untrust" />
                            <asp:LinkButton runat="server" CssClass="btn btn-danger btn-sm" CommandName="block"
                                CommandArgument='<%# Eval("DeviceId") %>' Text="Block" />
                            <asp:LinkButton runat="server" CssClass="btn btn-ghost btn-sm" CommandName="unblock"
                                CommandArgument='<%# Eval("DeviceId") %>' Text="Unblock" />
                        </td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
                <div class="empty">No devices have punched yet.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
