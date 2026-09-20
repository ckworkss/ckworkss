<%@ Page Title="My attendance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="MyAttendance.aspx.cs" Inherits="RemotePunch.Web.MyAttendancePage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>My attendance</h1>

    <div class="card">
        <div class="filters">
            <div class="field">
                <label for="<%= txtFrom.ClientID %>">From</label>
                <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" />
            </div>
            <div class="field">
                <label for="<%= txtTo.ClientID %>">To</label>
                <asp:TextBox ID="txtTo" runat="server" TextMode="Date" />
            </div>
            <div class="actions">
                <asp:Button ID="btnApply" runat="server" Text="Show" CssClass="btn btn-primary" OnClick="btnApply_Click" />
                <a class="btn btn-ghost" id="lnkExport" runat="server">Export CSV</a>
            </div>
        </div>
    </div>

    <div class="card">
        <h2>Daily summary</h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptDays" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>Date</th><th>First in</th><th>Last out</th>
                                <th class="num">Worked</th><th class="num">Late</th>
                                <th class="num">Punches</th><th class="num">Flagged</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td class="nowrap"><%# Eval("WorkDateLocal", "{0:ddd, dd MMM yyyy}") %></td>
                        <td><%# TimeText(Eval("FirstInLocal")) %></td>
                        <td><%# TimeText(Eval("LastOutLocal")) %></td>
                        <td class="num"><%# Eval("WorkedHoursText") %></td>
                        <td class="num"><%# LateText(Eval("LateMinutes")) %></td>
                        <td class="num"><%# Eval("PunchCount") %></td>
                        <td class="num"><%# Eval("FlaggedCount") %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNoDays" runat="server" Visible="false">
                <div class="empty">No attendance in this range.</div>
            </asp:PlaceHolder>
        </div>
    </div>

    <div class="card">
        <h2>Punch history</h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptPunches" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>When</th><th>Type</th><th>Status</th><th>Site</th>
                                <th class="num">Distance</th><th class="num">Accuracy</th>
                                <th>Where</th><th class="wrap">Notes</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr class="<%# RowClass(Eval("Status")) %>">
                        <td class="nowrap"><%# Eval("PunchTimeLocal", "{0:dd MMM, HH:mm:ss}") %></td>
                        <td><%# Eval("PunchType") %></td>
                        <td><span class="badge <%# BadgeClass(Eval("Status")) %>"><%# Eval("Status") %></span></td>
                        <td><%# PageHelpers.TextOrDash(Eval("SiteName")) %></td>
                        <td class="num"><%# DistanceText(Eval("DistanceMeters")) %></td>
                        <td class="num"><%# AccuracyText(Eval("AccuracyMeters")) %></td>
                        <td class="small"><%# MapLink(Eval("Latitude"), Eval("Longitude"), Eval("ResolvedAddress")) %></td>
                        <td class="wrap small muted"><%# ReasonText(Container.DataItem) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNoPunches" runat="server" Visible="false">
                <div class="empty">No punches in this range.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
