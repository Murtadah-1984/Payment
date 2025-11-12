import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const responseTime = new Trend('response_time');

// Stress test configuration - gradually increase load until system breaks
export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Normal load
    { duration: '2m', target: 100 },   // Increase load
    { duration: '2m', target: 200 },    // Double load
    { duration: '2m', target: 300 },    // Triple load
    { duration: '2m', target: 400 },   // High load
    { duration: '2m', target: 500 },   // Very high load
    { duration: '1m', target: 0 },      // Recovery
  ],
  thresholds: {
    http_req_duration: ['p(95)<5000'], // 95% of requests < 5s
    http_req_failed: ['rate<0.05'],    // Error rate < 5% (stress test allows higher)
    errors: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_VERSION = __ENV.API_VERSION || 'v1';
const JWT_TOKEN = __ENV.JWT_TOKEN || '';

function getHeaders() {
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };
  
  if (JWT_TOKEN) {
    headers['Authorization'] = `Bearer ${JWT_TOKEN}`;
  }
  
  return headers;
}

function generatePaymentData() {
  return {
    amount: Math.floor(Math.random() * 10000) + 100,
    currency: 'USD',
    paymentMethod: 'Card',
    provider: 'ZainCash',
    merchantId: `merchant-${Math.floor(Math.random() * 1000)}`,
    orderId: `order-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
    idempotencyKey: `idempotency-${Date.now()}-${Math.floor(Math.random() * 10000)}`,
  };
}

export default function () {
  const paymentData = generatePaymentData();
  const url = `${BASE_URL}/api/${API_VERSION}/payments`;
  
  const startTime = Date.now();
  const response = http.post(url, JSON.stringify(paymentData), {
    headers: getHeaders(),
    tags: { name: 'CreatePayment' },
  });
  const duration = Date.now() - startTime;
  
  responseTime.add(duration);
  
  const success = check(response, {
    'status is 201': (r) => r.status === 201,
  });
  
  if (!success) {
    errorRate.add(1);
  }
  
  sleep(0.5);
}

