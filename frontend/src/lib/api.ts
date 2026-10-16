import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

// One RTK Query API for the whole app. Each feature adds its own endpoints
// with api.injectEndpoints(). The Vite dev server forwards /api to the backend.
//
// Tags tell RTK Query which cached answers to reload after a change:
// "Lead" for lead data, "Events" for tracker events (one lead's, or { id: 'LIST' }
// for the Logs page), "Endpoint" for the tracker endpoint templates, "Inbound"
// for the pixel token on the settings page.
export const api = createApi({
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Lead', 'Events', 'Endpoint', 'Inbound'],
  endpoints: () => ({}),
});
