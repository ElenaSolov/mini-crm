import { api } from '../../../lib/api';
import type { EndpointPreview, EndpointTemplates, MacroList, PingResult, TrackerEndpoint } from '../types';

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
  usePingMutation,
} = settingsApi;
