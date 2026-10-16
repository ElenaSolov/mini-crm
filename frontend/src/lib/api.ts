import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

// One RTK Query API for the whole app. Each feature adds its own endpoints
// with api.injectEndpoints(). The Vite dev server forwards /api to the backend.
//
// Tags tell RTK Query which cached answers to reload after a change:
// "Lead" for lead data, "Events" for a lead's tracker events, "Endpoint" for
// the tracker endpoint templates on the settings page.
export const api = createApi({
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  tagTypes: ['Lead', 'Events', 'Endpoint'],
  endpoints: () => ({}),
});
