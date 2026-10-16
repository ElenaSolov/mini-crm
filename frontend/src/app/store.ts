import { configureStore } from '@reduxjs/toolkit';
import { api } from '../lib/api';

// The only global state is RTK Query's cache of API answers. Screen state
// (filters, which lead is open) stays in components with useState.
export const store = configureStore({
  reducer: {
    [api.reducerPath]: api.reducer,
  },
  middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(api.middleware),
});
