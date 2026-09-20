<%@ Page Title="Audit log" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Audit.aspx.cs" Inherits="RemotePunch.Web.Admin.AuditPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Audit log</h1>

    <div class="card">
        <div class="filters">
            <div class="field">
                <label for="<%= ddlEvent.ClientID %>">Event type</label>
                <asp:DropDownList ID="ddlEvent" runat="server">
                    <asp:ListItem Text="All events" Value="" />
                    <asp:ListItem Text="Sign-in succeeded" Value="LoginSucceeded" />
                    <asp:ListItem Text="Sign-in failed" Value="LoginFailed" />
                    <asp:ListItem Text="Sign-in blocked" Value="LoginBlocked" />
                    <asp:ListItem Text="Punch accepted" Value="PunchAccepted" />
                    <asp:ListItem Text="Punch flagged" Value="PunchFlagged" />
                    <asp:ListItem Text="Punch rejected" Value="PunchRejected" />
                    <asp:ListItem Text="Punch approved" Value="PunchApproved" />
                    <asp:ListItem Text="Punch declined" Value="PunchDeclined" />
                    <asp:ListItem Text="Password changed" Value="PasswordChanged" />
                    <asp:ListItem Text="Password reset" Value="PasswordReset" />
                    <asp:ListItem Text="Employee created" Value="EmployeeCreated" />
                    <asp:ListItem Text="Employee updated" Value="EmployeeUpdated" />
                    <asp:ListItem Text="Site created" Value="SiteCreated" />
                    <asp:ListItem Text="Site updated" Value="SiteUpdated" />
                    <asp:ListItem Text="Device updated" Value="DeviceUpdated" />
                    <asp:ListItem Text="Export" Value="Export" />
                    <asp:ListItem Text="Unhandled error" Value="UnhandledError" />
                </asp:DropDownList>
            </div>
            <div class="field">
                <label for="<%= txtRows.ClientID %>">Rows</label>
                <asp:TextBox ID="txtRows" runat="server" TextMode="Number" Text="200" />
            </div>
            <div class="actions">
                <asp:Button ID="btnApply" runat="server" Text="Show" CssClass="btn btn-primary" OnClick="btnApply_Click" />
            </div>
        </div>
    </div>

    <div class="card">
        <div class="table-wrap">
            <asp:Repeater ID="rptAudit" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr><th>When</th><th>Employee</th><th>Event</th><th class="wrap">Detail</th><th>IP</th></tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td class="nowrap small"><%# LocalTime(Eval("CreatedAtUtc")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("EmployeeName")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("EventType")) %></td>
                        <td class="wrap small muted"><%# PageHelpers.TextOrDash(Eval("Detail")) %></td>
                        <td class="mono small"><%# PageHelpers.TextOrDash(Eval("IpAddress")) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
                <div class="empty">Nothing logged for this filter.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
