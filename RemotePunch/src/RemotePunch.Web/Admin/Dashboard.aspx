<%@ Page Title="Admin dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Dashboard.aspx.cs" Inherits="RemotePunch.Web.Admin.DashboardPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Admin dashboard <span class="muted small">&mdash; <asp:Literal ID="litToday" runat="server" /></span></h1>

    <div class="grid grid-3">
        <div class="card stat"><div class="value"><asp:Literal ID="litPresent" runat="server" /></div><div class="label">Employees punched today</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litPunches" runat="server" /></div><div class="label">Punches today</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litFlagged" runat="server" /></div><div class="label">Flagged today</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litRejected" runat="server" /></div><div class="label">Rejected today</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litPending" runat="server" /></div><div class="label">Awaiting review</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litSites" runat="server" /></div><div class="label">Active sites</div></div>
    </div>

    <div class="card">
        <h2>Manage</h2>
        <div class="actions">
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Review.aspx") %>">Review queue</a>
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Employees.aspx") %>">Employees</a>
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Sites.aspx") %>">Sites &amp; geofences</a>
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Devices.aspx") %>">Devices</a>
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Admin/Audit.aspx") %>">Audit log</a>
            <a class="btn btn-ghost" href="<%= ResolveUrl("~/Reports.aspx") %>">Reports</a>
        </div>
    </div>

    <div class="card">
        <h2>Latest punches today</h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptRecent" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>When</th><th>Employee</th><th>Type</th><th>Status</th><th>Site</th>
                                <th class="num">Distance</th><th class="num">Risk</th><th class="wrap">Notes</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr class="<%# PageHelpers.RowClass(Eval("Status")) %>">
                        <td class="nowrap"><%# Eval("PunchTimeLocal", "{0:HH:mm:ss}") %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("EmployeeName")) %></td>
                        <td><%# Eval("PunchType") %></td>
                        <td><span class="badge <%# PageHelpers.BadgeClass(Eval("Status")) %>"><%# Eval("Status") %></span></td>
                        <td><%# PageHelpers.TextOrDash(Eval("SiteName")) %></td>
                        <td class="num"><%# PageHelpers.DistanceText(Eval("DistanceMeters")) %></td>
                        <td class="num"><%# Eval("RiskScore") %></td>
                        <td class="wrap small muted"><%# PageHelpers.ReasonText((RemotePunch.Web.Models.Punch)Container.DataItem) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNone" runat="server" Visible="false">
                <div class="empty">No punches today yet.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
