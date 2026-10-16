import { api } from '../../../lib/api';
import type { TrackerEventPage, TrackerEventsQuery } from '../types';

export const eventsApi = api.injectEndpoints({
  endpoints: (build) => ({
    // { type: 'Events', id: 'LIST' }: converting a lead marks it, so the Logs page reloads.
    getEvents: build.query<TrackerEventPage, TrackerEventsQuery>({
      query: (params) => ({ url: 'events', params }),
      providesTags: [{ type: 'Events', id: 'LIST' }],
    }),
  }),
});

export const { useGetEventsQuery } = eventsApi;
