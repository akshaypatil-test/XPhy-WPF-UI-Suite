#pragma once

#include <deque>
#include <vector>

namespace edf::utils {
template <typename T> class BoundedDeque {
  public:
    explicit BoundedDeque(size_t size) : maxSize_(size) {}

    explicit BoundedDeque(T value, size_t size) : maxSize_(size) { fill(value); }

    void append(T element) {
        if (elements_.size() == maxSize_) {
            elements_.pop_front();
        }
        elements_.push_back(element);
    }

    void append(const std::vector<T>& newElements) {
        auto totalSize = elements_.size() + newElements.size();
        if (totalSize > maxSize()) {
            drop_front(totalSize - maxSize());
        }
        elements_.insert(elements_.end(), newElements.begin(), newElements.end());
    }

    void drop_front(size_t discardSize) { elements_ = {elements_.begin() + discardSize, elements_.end()}; }

    void clear() { elements_.clear(); }

    size_t size() const { return elements_.size(); }

    size_t maxSize() const { return maxSize_; }

    bool empty() const { return elements_.empty(); }

    const std::deque<T>& deque() const { return elements_; }

    void fill(T value) {
        for (auto _ = maxSize(); _--;) {
            append(value);
        }
    }

  private:
    const size_t maxSize_;
    std::deque<T> elements_;
};
} // namespace edf::utils
