<%@ Page Title="Reports" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Reports.aspx.cs" Inherits="RemotePunch.Web.ReportsPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Attendance reports</h1>

    <div class="card">
        <div class="filters">
            <div class="field">
                <label for="<%= ddlEmployee.ClientID %>">Employee</label>
                <asp:DropDownList ID="ddlEmployee" runat="server" />
            </div>
            <div class="field">
                <label for="<%= txtFrom.ClientID %>">From</label>
                <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" />
            </div>
            <div class="field">
                <label for="<%= txtTo.ClientID %>">To</label>
                <asp:TextBox ID="txtTo" runat="server" TextMode="Date" />
            </div>
            <div class="field">
                <label for="<%= ddlStatus.ClientID %>">Punch status</label>
                <asp:DropDownList ID="ddlStatus" runat="server">
                    <asp:ListItem Text="All" Value="" />
                    <asp:ListItem Text="Accepted" Value="Accepted" />
                    <asp:ListItem Text="Flagged" Value="Flagged" />
                    <asp:ListItem Text="Rejected" Value="Rejected" />
                </asp:DropDownList>
            </div>
            <div class="actions">
                <asp:Button ID="btnApply" runat="server" Text="Show" CssClass="btn btn-primary" OnClick="btnApply_Click" />
                <a class="btn btn-ghost" id="lnkExportDays" runat="server">Export days</a>
                <a class="btn btn-ghost" id="lnkExportPunches" runat="server">Export punches</a>
            </div>
        </div>
    </div>

    <div class="grid grid-3">
        <div class="card stat"><div class="value"><asp:Literal ID="litTotalDays" runat="server" /></div><div class="label">Employee-days</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litTotalHours" runat="server" /></div><div class="label">Hours worked</div></div>
        <div class="card stat"><div class="value"><asp:Literal ID="litFlagged" runat="server" /></div><div class="label">Flagged punches</div></div>
    </div>

    <div class="card">
        <h2>Daily attendance</h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptDays" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>Date</th><th>Code</th><th>Employee</th><th>First in</th><th>Last out</th>
                                <th class="num">Worked</th><th class="num">Late</th><th class="num">Punches</th><th class="num">Flagged</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td class="nowrap"><%# Eval("WorkDateLocal", "{0:dd MMM yyyy}") %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("EmployeeCode")) %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("FullName")) %></td>
                        <td><%# PageHelpers.TimeText(Eval("FirstInLocal")) %></td>
                        <td><%# PageHelpers.TimeText(Eval("LastOutLocal")) %></td>
                        <td class="num"><%# Eval("WorkedHoursText") %></td>
                        <td class="num"><%# PageHelpers.LateText(Eval("LateMinutes")) %></td>
                        <td class="num"><%# Eval("PunchCount") %></td>
                        <td class="num"><%# Eval("FlaggedCount") %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNoDays" runat="server" Visible="false">
                <div class="empty">Nothing recorded in this range.</div>
            </asp:PlaceHolder>
        </div>
    </div>

    <div class="card">
        <h2>Punches <span class="muted small">(most recent 500)</span></h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptPunches" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>When</th><th>Employee</th><th>Type</th><th>Status</th><th>Site</th>
                                <th class="num">Distance</th><th class="num">Accuracy</th><th class="num">Risk</th>
                                <th>Where</th><th class="wrap">Notes</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr class="<%# PageHelpers.RowClass(Eval("Status")) %>">
                        <td class="nowrap"><%# Eval("PunchTimeLocal", "{0:dd MMM, HH:mm:ss}") %></td>
                        <td><%# PageHelpers.TextOrDash(Eval("EmployeeName")) %></td>
                        <td><%# Eval("PunchType") %></td>
                        <td><span class="badge <%# PageHelpers.BadgeClass(Eval("Status")) %>"><%# Eval("Status") %></span></td>
                        <td><%# PageHelpers.TextOrDash(Eval("SiteName")) %></td>
                        <td class="num"><%# PageHelpers.DistanceText(Eval("DistanceMeters")) %></td>
                        <td class="num"><%# PageHelpers.AccuracyText(Eval("AccuracyMeters")) %></td>
                        <td class="num"><%# Eval("RiskScore") %></td>
                        <td class="small"><%# PageHelpers.MapLink(Eval("Latitude"), Eval("Longitude"), Eval("ResolvedAddress")) %></td>
                        <td class="wrap small muted"><%# PageHelpers.ReasonText((RemotePunch.Web.Models.Punch)Container.DataItem) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNoPunches" runat="server" Visible="false">
                <div class="empty">No punches match these filters.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
