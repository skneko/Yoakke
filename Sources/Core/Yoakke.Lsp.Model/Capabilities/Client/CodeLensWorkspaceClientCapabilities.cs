// Copyright (c) 2021 Yoakke.
// Licensed under the Apache License, Version 2.0.
// Source repository: https://github.com/LanguageDev/Yoakke

using Newtonsoft.Json;

namespace Yoakke.Lsp.Model.Capabilities.Client
{
    /// <summary>
    /// See https://microsoft.github.io/language-server-protocol/specifications/specification-current/#codeLensWorkspaceClientCapabilities.
    /// </summary>
    public class CodeLensWorkspaceClientCapabilities
    {
        /// <summary>
        /// Whether the client implementation supports a refresh request sent from the
        /// server to the client.
        ///
        /// Note that this event is global and will force the client to refresh all
        /// code lenses currently shown. It should be used with absolute care and is
        /// useful for situation where a server for example detect a project wide
        /// change that requires such a calculation.
        /// </summary>
        [JsonProperty("refreshSupport", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RefreshSupport { get; set; }
    }
}
