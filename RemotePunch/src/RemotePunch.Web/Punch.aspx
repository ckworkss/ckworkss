<%@ Page Title="Punch" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Punch.aspx.cs" Inherits="RemotePunch.Web.PunchPage" %>

<asp:Content ID="head" ContentPlaceHolderID="head" runat="server">
    <%-- Leaflet draws the map. If the CDN is unreachable the page still works:
         punch.js checks for window.L and simply hides the map. To run fully
         offline, drop leaflet.css/leaflet.js into ~/Content and repoint these
         two tags (and add an integrity hash for the version you pin). --%>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.css"
          crossorigin="anonymous" referrerpolicy="no-referrer" />
</asp:Content>

<asp:Content ID="body" ContentPlaceHolderID="MainContent" runat="server">

    <h1>Punch <span class="muted small">&mdash; <asp:Literal ID="litToday" runat="server" /></span></h1>

    <div id="rpAlert" class="alert alert-info" role="status">
        Getting your location&hellip; keep this page open until the reading settles.
    </div>

    <div class="grid grid-punch">

        <!-- ------------------------------------------------ punch card -->
        <div class="card">
            <div id="stateBox" class="punch-state <%= IsPunchedIn ? "in" : "out" %>">
                <span class="dot"></span>
                <div>
                    <div><strong><%= IsPunchedIn ? "You are punched in" : "You are punched out" %></strong></div>
                    <div class="muted small"><asp:Literal ID="litLastPunch" runat="server" /></div>
                </div>
            </div>

            <div class="field">
                <label for="noteBox">Note <span class="muted">(required when you are away from your site)</span></label>
                <textarea id="noteBox" maxlength="500" placeholder="e.g. client visit at Nehru Place"></textarea>
            </div>

            <asp:PlaceHolder ID="phSelfie" runat="server">
                <div id="selfieWrap" class="field">
                    <label>Photo <span class="muted">(required on this account)</span></label>
                    <video id="selfieVideo" playsinline muted autoplay></video>
                    <img id="selfiePreview" alt="Captured photo" style="display:none" />
                    <div class="actions">
                        <button type="button" id="btnCamera" class="btn btn-ghost btn-sm">Start camera</button>
                        <button type="button" id="btnCapture" class="btn btn-ghost btn-sm" disabled>Capture</button>
                        <button type="button" id="btnRetake" class="btn btn-ghost btn-sm" style="display:none">Retake</button>
                    </div>
                </div>
            </asp:PlaceHolder>

            <button type="button" id="btnPunch" class="btn btn-primary btn-punch" disabled>
                Waiting for location&hellip;
            </button>

            <div class="actions" style="margin-top:12px">
                <button type="button" id="btnRefreshFix" class="btn btn-ghost btn-sm">Refresh location</button>
                <a class="btn btn-ghost btn-sm" href="<%= ResolveUrl("~/MyAttendance.aspx") %>">My attendance</a>
            </div>

            <div id="queueNotice" class="alert alert-warn small" style="display:none;margin-top:12px"></div>
        </div>

        <!-- -------------------------------------------------- geo card -->
        <div class="card">
            <h2>Location sensing</h2>
            <div id="map"></div>

            <div class="accuracy-bar" id="accuracyBar"><span></span></div>

            <div class="geo-readout">
                <div class="readout-row"><span class="k">Nearest site</span><span class="v" id="roSite">&mdash;</span></div>
                <div class="readout-row"><span class="k">Distance</span><span class="v" id="roDistance">&mdash;</span></div>
                <div class="readout-row"><span class="k">Accuracy</span><span class="v" id="roAccuracy">&mdash;</span></div>
                <div class="readout-row"><span class="k">Latitude, longitude</span><span class="v mono" id="roCoords">&mdash;</span></div>
                <div class="readout-row"><span class="k">Altitude</span><span class="v" id="roAltitude">&mdash;</span></div>
                <div class="readout-row"><span class="k">Speed / heading</span><span class="v" id="roMotion">&mdash;</span></div>
                <div class="readout-row"><span class="k">Fix age</span><span class="v" id="roAge">&mdash;</span></div>
                <div class="readout-row"><span class="k">Readings taken</span><span class="v" id="roSamples">0</span></div>
            </div>

            <p class="small muted" style="margin-top:12px">
                Your position is checked again on the server when you punch, so the
                distance shown here is only a preview.
            </p>
        </div>
    </div>

    <!-- --------------------------------------------------- today's list -->
    <div class="card">
        <h2>Today</h2>
        <div class="table-wrap">
            <asp:Repeater ID="rptToday" runat="server">
                <HeaderTemplate>
                    <table class="data">
                        <thead>
                            <tr>
                                <th>Time</th><th>Type</th><th>Status</th><th>Site</th>
                                <th class="num">Distance</th><th class="wrap">Notes</th>
                            </tr>
                        </thead>
                        <tbody>
                </HeaderTemplate>
                <ItemTemplate>
                    <tr class="<%# RowClass(Eval("Status")) %>">
                        <td class="nowrap"><%# Eval("PunchTimeLocal", "{0:HH:mm:ss}") %></td>
                        <td><%# Eval("PunchType") %></td>
                        <td><span class="badge <%# BadgeClass(Eval("Status")) %>"><%# Eval("Status") %></span></td>
                        <td><%# PageHelpers.TextOrDash(Eval("SiteName")) %></td>
                        <td class="num"><%# DistanceText(Eval("DistanceMeters")) %></td>
                        <td class="wrap small muted"><%# ReasonText(Container.DataItem) %></td>
                    </tr>
                </ItemTemplate>
                <FooterTemplate>
                        </tbody>
                    </table>
                </FooterTemplate>
            </asp:Repeater>
            <asp:PlaceHolder ID="phNoPunches" runat="server">
                <div class="empty">No punches recorded for today yet.</div>
            </asp:PlaceHolder>
        </div>
        <p class="small muted" style="margin-top:10px">
            Worked so far today: <strong><asp:Literal ID="litWorked" runat="server" /></strong>
        </p>
    </div>

    <script src="https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/leaflet.min.js"
            crossorigin="anonymous" referrerpolicy="no-referrer"></script>
    <script>window.RP_CONFIG = <%= ConfigJson %>;</script>
    <script src="<%= ResolveUrl("~/Scripts/punch.js") %>"></script>
</asp:Content>
