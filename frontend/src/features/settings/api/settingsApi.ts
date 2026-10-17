import { api } from '../../../lib/api';
import type {
  EndpointPreview,
  EndpointTemplates,
  InboundSettings,
  MacroList,
  PingResult,
  SaveTrackerAuth,
  TrackerAuth,
  TrackerEndpoint,
} from '../types';

export const settingsApi = api.injectEndpoints({
  endpoints: (build) => ({
    getEndpoints: build.query<TrackerEndpoint[], void>({
      query: () => 'tracker/endpoints',
      providesTags: ['Endpoint'],
    }),

    getMacros: build.query<MacroList, void>({
      query: () => 'tracker/macros',
    }),

    // A POST, but it saves nothing, so it is a query: RTK Query caches it per input.
    previewEndpoint: build.query<EndpointPreview, { id: number; templates: EndpointTemplates }>({
      query: ({ id, templates }) => ({ url: `tracker/endpoints/${id}/preview`, method: 'POST', body: templates }),
    }),

    updateEndpoint: build.mutation<TrackerEndpoint, { id: number; templates: EndpointTemplates }>({
      query: ({ id, templates }) => ({ url: `tracker/endpoints/${id}`, method: 'PUT', body: templates }),
      invalidatesTags: ['Endpoint'],
    }),

    resetEndpoint: build.mutation<TrackerEndpoint, number>({
      query: (id) => ({ url: `tracker/endpoints/${id}/reset`, method: 'POST' }),
      invalidatesTags: ['Endpoint'],
    }),

    getTrackerAuth: build.query<TrackerAuth, void>({
      query: () => 'tracker/auth',
      providesTags: ['Auth'],
    }),

    updateTrackerAuth: build.mutation<TrackerAuth, SaveTrackerAuth>({
      query: (auth) => ({ url: 'tracker/auth', method: 'PUT', body: auth }),
      invalidatesTags: ['Auth'],
    }),

    resetTrackerAuth: build.mutation<TrackerAuth, void>({
      query: () => ({ url: 'tracker/auth/reset', method: 'POST' }),
      invalidatesTags: ['Auth'],
    }),

    // After this the key from .env is used again.
    clearTrackerKey: build.mutation<TrackerAuth, void>({
      query: () => ({ url: 'tracker/auth/key', method: 'DELETE' }),
      invalidatesTags: ['Auth'],
    }),

    getInboundSettings: build.query<InboundSettings, void>({
      query: () => 'inbound/settings',
      providesTags: ['Inbound'],
    }),

    // The old token stops working at once, so the URLs on the page reload with the new one.
    setInboundToken: build.mutation<InboundSettings, string>({
      query: (token) => ({ url: 'inbound/token', method: 'PUT', body: { token } }),
      invalidatesTags: ['Inbound'],
    }),

    generateInboundToken: build.mutation<InboundSettings, void>({
      query: () => ({ url: 'inbound/token/generate', method: 'POST' }),
      invalidatesTags: ['Inbound'],
    }),

    // A mutation, not a query: every click should call the tracker again, not show a cached answer.
    ping: build.mutation<PingResult, void>({
      query: () => 'tracker/ping',
    }),
  }),
});

export const {
  useGetEndpointsQuery,
  useGetMacrosQuery,
  usePreviewEndpointQuery,
  useUpdateEndpointMutation,
  useResetEndpointMutation,
  useGetTrackerAuthQuery,
  useUpdateTrackerAuthMutation,
  useResetTrackerAuthMutation,
  useClearTrackerKeyMutation,
  useGetInboundSettingsQuery,
  useSetInboundTokenMutation,
  useGenerateInboundTokenMutation,
  usePingMutation,
} = settingsApi;
