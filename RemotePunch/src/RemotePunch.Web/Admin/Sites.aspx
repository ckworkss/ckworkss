<%@ Page Title="Sites" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Sites.aspx.cs" Inherits="RemotePunch.Web.Admin.SitesPage" %>

<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <h1>Sites &amp; geofences</h1>
    <p class="muted">
        Each site is a circle. A punch counts as on-site when its distance to the
        centre is within the radius.
    </p>

    <div class="card">
        <div class="actions">
            <a class="btn btn-primary" href="<%= ResolveUrl("~/Admin/SiteEdit.aspx") %>">Add site</a>
            <asp:CheckBox ID="chkIncludeInactive" runat="server" AutoPostBack="true"
                          Text=" Show inactive sites" OnCheckedChanged="chkIncludeInactive_CheckedChanged" />
        </div>
    </div>

    <div class="card">
        <div class="table-wrap">
            <asp:Repeater ID="rptSites" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr><th>Name</th><th>Address</th><th>Centre</th><th class="num">Radius</th><th>Status</th><th></th></tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr>
                        <td><%# PageHelpers.TextOrDash(Eval("Name")) %></td>
                        <td class="small"><%# PageHelpers.TextOrDash(Eval("Address")) %></td>
                        <td class="small"><%# PageHelpers.MapLink(Eval("Latitude"), Eval("Longitude"), null) %></td>
                        <td class="num"><%# Eval("RadiusMeters") %> m</td>
                        <td><%# Convert.ToBoolean(Eval("IsActive"))
                                ? "<span class=\"badge badge-ok\">active</span>"
                                : "<span class=\"badge badge-muted\">inactive</span>" %></td>
                        <td><a class="btn btn-ghost btn-sm"
                               href='<%# ResolveUrl("~/Admin/SiteEdit.aspx?id=") + Eval("SiteId") %>'>Edit</a></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate></tbody></table></FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
                <div class="empty">No sites yet. Add one so punches can be matched to a location.</div>
            </asp:PlaceHolder>
        </div>
    </div>
</asp:Content>
